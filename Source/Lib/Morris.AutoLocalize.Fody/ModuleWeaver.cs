using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Morris.AutoLocalize.Fody.Extensions;
using Morris.AutoLocalize.Fody.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;

namespace Morris.AutoLocalize.Fody;

public class ModuleWeaver : BaseModuleWeaver
{
	public override IEnumerable<string> GetAssembliesForScanning()
	{
		yield return "netstandard";
		yield return "mscorlib";
		yield return "System.ComponentModel.Annotations";
	}

	public override void Execute()
	{
		var discoveredItems = new ErrorMessageResourceTypes();
		AutoLocalizeValidationAttributesAttributeData? attributeData = GetValidationAttributeData();
		if (attributeData is not null)
		{
			TypeDefinition? validationAttributeType = GetValidationAttributeType();
			if (validationAttributeType is not null)
				ProcessClasses(discoveredItems, validationAttributeType, attributeData);
		}
		RemoveDependency();
	}

	private AutoLocalizeValidationAttributesAttributeData? GetValidationAttributeData()
	{
		CustomAttribute? attribute =
			ModuleDefinition
			.Assembly
			.CustomAttributes
			.FirstOrDefault(x =>
				x.AttributeType.FullName == typeof(AutoLocalizeValidationAttributesAttribute).FullName
			);
		if (attribute is null)
			return null;
		ModuleDefinition.Assembly.CustomAttributes.Remove(attribute);

		Dictionary<string, object?> values = attribute.GetValues();
		return AutoLocalizeValidationAttributesAttributeData.FromDictionary(ModuleDefinition, values);
	}

	private TypeDefinition? GetValidationAttributeType()
	{
		AssemblyNameReference annotationsReference =
			ModuleDefinition
			.AssemblyReferences
			.FirstOrDefault(x => x.Name == "System.ComponentModel.Annotations");
		if (annotationsReference is null)
			return null;

		AssemblyDefinition annotationsAssembly =
			ModuleDefinition.AssemblyResolver.Resolve(annotationsReference);

		TypeDefinition validationAttributeTypeDefinition =
			annotationsAssembly
			.MainModule
			.GetType("System.ComponentModel.DataAnnotations.ValidationAttribute");

		return validationAttributeTypeDefinition;
	}

	private void ProcessClasses(
		ErrorMessageResourceTypes discoveredItems,
		TypeDefinition validationAttributeType,
		AutoLocalizeValidationAttributesAttributeData attributeData)
	{
		IEnumerable<TypeDefinition> classesToScan =
			ModuleDefinition
			.GetAllTypes()
			.Where(x => x.IsClass)
			.Where(x => !x.Name.StartsWith("<"));

		foreach (TypeDefinition type in classesToScan)
			ScanType(
				discoveredItems,
				validationAttributeType,
				attributeData,
				type);

		EnsureRequiredResourceNamesExistInResourceFiles(discoveredItems);
		WriteManifestFile(attributeData.ErrorMessageResourceType, discoveredItems);
	}

	private void ScanType(
		ErrorMessageResourceTypes discoveredItems,
		TypeDefinition validationAttributeType,
		AutoLocalizeValidationAttributesAttributeData attributeData,
		TypeDefinition type)
	{
		IEnumerable<IMemberDefinition> memberDefinitions = [.. type.Fields, .. type.Properties];
		memberDefinitions = memberDefinitions.Where(x => !x.Name.StartsWith("<"));
		foreach (IMemberDefinition memberDefinition in memberDefinitions)
			ScanMember(
				discoveredItems,
				validationAttributeType,
				attributeData,
				memberDefinition
			);
	}

	private void ScanMember(
		ErrorMessageResourceTypes discoveredItems,
		TypeDefinition validationAttributeType,
		AutoLocalizeValidationAttributesAttributeData attributeData,
		IMemberDefinition memberDefinition)
	{
		IEnumerable<ValidationAttributeInfo> validationAttributes =
			memberDefinition
			.CustomAttributes
			.Where(x => x.AttributeType.IsAssignableTo(validationAttributeType))
			.Select(x => new
			{
				ValidationAttribute = x,
				AttributeValues = x.GetValues()
			}
			)
			.Where(x =>
				(!x.AttributeValues.TryGetValue("ErrorMessage", out object? errorMessage) || errorMessage is null)
				&& (!x.AttributeValues.TryGetValue("ErrorMessageResourceType", out object? errorMessageResourceType) || errorMessageResourceType is null)
			)
			.Select(x =>
				new ValidationAttributeInfo(
					x.ValidationAttribute,
					x.AttributeValues.TryGetValue("ErrorMessageResourceType", out object? type) ? (TypeReference?)type : null,
					x.AttributeValues.TryGetValue("ErrorMessageResourceName", out object? name) ? (string?)name : null
				)
			);

		foreach (ValidationAttributeInfo validationAttribute in validationAttributes)
			UpdateValidationAttribute(
				discoveredItems,
				memberDefinition,
				attributeData,
				validationAttribute);
	}

	private void UpdateValidationAttribute(
		ErrorMessageResourceTypes discoveredItems,
		IMemberDefinition memberDefinition,
		AutoLocalizeValidationAttributesAttributeData attributeData,
		ValidationAttributeInfo validationAttributeInfo)
	{
		string attributeTypeName = validationAttributeInfo.ValidationAttribute.AttributeType.Name;

		string resourceKeySuffix = StringHelper.GetAttributeShortName(attributeTypeName);
		string resourceName = attributeData.ErrorMessageResourceNamePrefix + resourceKeySuffix;

		TypeReference systemTypeReference = FindTypeDefinition("System.Type");

		validationAttributeInfo
			.ValidationAttribute
			.Properties
			.AddOrReplace(
				new CustomAttributeNamedArgument(
					"ErrorMessageResourceType",
					new CustomAttributeArgument(
						type: systemTypeReference,
						value: attributeData.ErrorMessageResourceType
					)
				)
			);

		if (validationAttributeInfo.ErrorMessageResourceName is not null)
		{
			resourceName = validationAttributeInfo.ErrorMessageResourceName;
		}
		else
		{
			validationAttributeInfo
				.ValidationAttribute
				.Properties
				.AddOrReplace(
					new CustomAttributeNamedArgument(
						"ErrorMessageResourceName",
						new CustomAttributeArgument(
							type: ModuleDefinition.TypeSystem.String,
							value: resourceName
						)
					)
				);
		}

		UpdateDiscoveredItems(
			discoveredItems: discoveredItems,
			memberDefinition: memberDefinition,
			validationAttribute: validationAttributeInfo.ValidationAttribute,
			errorMessageResourceType: attributeData.ErrorMessageResourceType,
			errorMessageResourceName: resourceName
		);
	}

	private static void UpdateDiscoveredItems(
		ErrorMessageResourceTypes discoveredItems,
		IMemberDefinition memberDefinition,
		CustomAttribute validationAttribute,
		TypeReference errorMessageResourceType,
		string errorMessageResourceName)
	{
		if (!discoveredItems.TryGetValue(errorMessageResourceType, out ErrorMessageResourceNames names))
		{
			names = new ErrorMessageResourceNames();
			discoveredItems.Add(errorMessageResourceType, names);
		}
		if (names.TryGetValue(errorMessageResourceName, out SequencePoint? sequencePoint) && sequencePoint is not null)
			return;

		sequencePoint = memberDefinition.GetSequencePoint();
		names[errorMessageResourceName] = sequencePoint;
	}

	private void EnsureRequiredResourceNamesExistInResourceFiles(ErrorMessageResourceTypes discoveredItems)
	{
		foreach(KeyValuePair<TypeReference, ErrorMessageResourceNames> resourceTypeAndNames in discoveredItems)
		{
			HashSet<string> actualKeysInResourceFile = GetActualResourceNamesForResourceType(resourceTypeAndNames.Key);
			foreach (KeyValuePair<string, SequencePoint?> nameAndLocation in resourceTypeAndNames.Value)
			{
				if (!actualKeysInResourceFile.Contains(nameAndLocation.Key))
				{
					string message = ErrorFactory.CreateErrorMessageResourceNameNotFoundError(resourceTypeAndNames.Key, nameAndLocation.Key);
					WriteError(message, nameAndLocation.Value);
				}
			}
		}
	}


	private void WriteManifestFile(
		TypeReference errorMessageResourceType,
		ErrorMessageResourceTypes discoveredItems)
	{
		string manifestFilePath = Path.ChangeExtension(ProjectFilePath, "Morris.AutoLocalize.ValidationAttributes.csv");
		if (File.Exists(manifestFilePath))
			File.Delete(manifestFilePath);

		var builder = new StringBuilder();
		builder.AppendLine("ErrorMessageResourceName");
		if (discoveredItems.TryGetValue(errorMessageResourceType, out ErrorMessageResourceNames? namesAndLocations))
		{
			foreach (var requiredResourceName in namesAndLocations.Keys.OrderBy(x => x))
				builder.AppendLine(requiredResourceName);
		}
		File.WriteAllText(manifestFilePath, builder.ToString());
	}

	private static HashSet<string> GetActualResourceNamesForResourceType(TypeReference errorMessageResourceType)
	{
		TypeDefinition errorMessageResourceTypeDefinition = errorMessageResourceType.Resolve();
		if (errorMessageResourceTypeDefinition is null)
			throw new WeavingException($"Could not resolve type for {nameof(errorMessageResourceType)} \"{errorMessageResourceType.FullName}\".");

		AssemblyDefinition resourceAssembly = errorMessageResourceTypeDefinition.Module.Assembly;

		string resourceBaseName = errorMessageResourceType.FullName.Replace('/', '+');
		string expectedResourcesName = $"{resourceBaseName}.resources";

		EmbeddedResource resourcesFile = resourceAssembly
			.MainModule
			.Resources
			.OfType<EmbeddedResource>()
			.SingleOrDefault(x => x.Name == expectedResourcesName);
		if (resourcesFile is null)
			throw new WeavingException($"Could not find Embedded resources \"{expectedResourcesName}\" for type \"{errorMessageResourceType.FullName}\".");

		var actualResourceNames = new HashSet<string>(StringComparer.Ordinal);
		using (Stream resourceStream = resourcesFile.GetResourceStream())
		{
			using var reader = new ResourceReader(resourceStream);
			IDictionaryEnumerator enumerator = reader.GetEnumerator();
			while (enumerator.MoveNext())
				actualResourceNames.Add((string)enumerator.Key);
		}

		return actualResourceNames;
	}

	private void RemoveDependency()
	{
		AssemblyNameReference? assemblyReference =
			ModuleDefinition
			.AssemblyReferences
			.FirstOrDefault(x => x.Name == "Morris.AutoLocalize");

		if (assemblyReference is not null)
			ModuleDefinition.AssemblyReferences.Remove(assemblyReference);
	}

	private readonly struct ValidationAttributeInfo
	{
		public CustomAttribute ValidationAttribute { get; }
		public string? ErrorMessageResourceName { get; }
		public TypeReference? ErrorMessageResourceType { get; }

		public ValidationAttributeInfo(
			CustomAttribute validationAttribute,
			TypeReference? errorMessageResourceType,
			string? errorMessageResourceName)
		{
			ValidationAttribute = validationAttribute ?? throw new ArgumentNullException(nameof(validationAttribute));
			ErrorMessageResourceName = errorMessageResourceName;
			ErrorMessageResourceType = errorMessageResourceType;
		}
	}

	private class ErrorMessageResourceTypes : Dictionary<TypeReference, ErrorMessageResourceNames>;
	private class ErrorMessageResourceNames : Dictionary<string, SequencePoint?>
	{
		public ErrorMessageResourceNames() : base(StringComparer.OrdinalIgnoreCase) { }
	}
}
