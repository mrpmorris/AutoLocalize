using Fody;
using Mono.Cecil;
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
		AutoLocalizeValidationAttributesAttributeData? attributeData = GetValidationAttributeData();
		if (attributeData is not null)
		{
			TypeDefinition? validationAttributeType = GetValidationAttributeType();
			if (validationAttributeType is not null)
				ProcessClasses(validationAttributeType, attributeData);
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
		TypeDefinition validationAttributeType,
		AutoLocalizeValidationAttributesAttributeData attributeData)
	{
		IEnumerable<TypeDefinition> classesToScan =
			ModuleDefinition
			.GetAllTypes()
			.Where(x => x.IsClass)
			.Where(x => !x.Name.StartsWith("<"));

		var requiredResourceNames = new HashSet<string>(StringComparer.Ordinal);
		var manuallyConfiguredResources = new List<(TypeReference ResourceType, string ResourceName)>();
		foreach (TypeDefinition type in classesToScan)
			ScanType(validationAttributeType, requiredResourceNames, manuallyConfiguredResources, attributeData, type);

		EnsureRequiredResourceNamesExistInResourceFile(attributeData.ErrorMessageResourceType, requiredResourceNames);

		foreach (IGrouping<string, (TypeReference ResourceType, string ResourceName)> group in manuallyConfiguredResources.GroupBy(x => x.ResourceType.FullName))
		{
			TypeReference resourceType = group.First().ResourceType;
			var resourceNames = new HashSet<string>(group.Select(x => x.ResourceName), StringComparer.Ordinal);
			EnsureRequiredResourceNamesExistInResourceFile(resourceType, resourceNames);
		}

		WriteManifestFile(requiredResourceNames);
	}

	private void ScanType(
		TypeDefinition validationAttributeType,
		HashSet<string> requiredResourceNames,
		List<(TypeReference ResourceType, string ResourceName)> manuallyConfiguredResources,
		AutoLocalizeValidationAttributesAttributeData attributeData,
		TypeDefinition type)
	{
		IEnumerable<IMemberDefinition> memberDefinitions = [.. type.Fields, .. type.Properties];
		memberDefinitions = memberDefinitions.Where(x => !x.Name.StartsWith("<"));
		foreach (IMemberDefinition memberDefinition in memberDefinitions)
			ScanMember(
				validationAttributeType,
				requiredResourceNames,
				manuallyConfiguredResources,
				attributeData,
				memberDefinition
			);
	}

	private void ScanMember(
		TypeDefinition validationAttributeType,
		HashSet<string> requiredResourceNames,
		List<(TypeReference ResourceType, string ResourceName)> manuallyConfiguredResources,
		AutoLocalizeValidationAttributesAttributeData attributeData,
		IMemberDefinition memberDefinition)
	{
		var validationAttributeDataList =
			memberDefinition
			.CustomAttributes
			.Where(x => x.AttributeType.IsAssignableTo(validationAttributeType))
			.Select(x => new { Attribute = x, Values = x.GetValues() })
			.ToList();

		IEnumerable<ValidationAttributeInfo> autoConfiguredAttributes =
			validationAttributeDataList
			.Where(x =>
				(!x.Values.TryGetValue("ErrorMessageResourceType", out object? resourceType) || resourceType is null)
				&&
				(!x.Values.TryGetValue("ErrorMessage", out object? errorMessage) || errorMessage is null)
			)
			.Select(x =>
				new ValidationAttributeInfo(
					x.Attribute,
					x.Values.TryGetValue("ErrorMessageResourceName", out object? value) ? (string?)value : null
				)
			);

		foreach (ValidationAttributeInfo validationAttribute in autoConfiguredAttributes)
			UpdateValidationAttribute(attributeData, requiredResourceNames, validationAttribute);

		foreach (var validationAttributeData in validationAttributeDataList)
		{
			if (!validationAttributeData.Values.TryGetValue("ErrorMessageResourceType", out object? resourceTypeObj) || resourceTypeObj is not TypeReference typeRef)
				continue;

			if (!validationAttributeData.Values.TryGetValue("ErrorMessageResourceName", out object? resourceNameObj) || resourceNameObj is not string name || string.IsNullOrEmpty(name))
				continue;

			if (validationAttributeData.Values.TryGetValue("ErrorMessage", out object? errorMessageObj) && errorMessageObj is not null)
				continue;

			manuallyConfiguredResources.Add((typeRef, name));
		}
	}

	private void UpdateValidationAttribute(
		AutoLocalizeValidationAttributesAttributeData attributeData,
		HashSet<string> requiredResourceNames,
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

		if (!string.IsNullOrEmpty(validationAttributeInfo.ErrorMessageResourceName))
		{
			requiredResourceNames.Add(validationAttributeInfo.ErrorMessageResourceName!);
		}
		else
		{
			requiredResourceNames.Add(resourceName);
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
	}

	private void EnsureRequiredResourceNamesExistInResourceFile(
		TypeReference errorMessageResourceType,
		IEnumerable<string> requiredResourceNames)
	{
		HashSet<string> actualKeysInResourceFile = GetActualResourceNamesForResourceType(errorMessageResourceType);
		foreach (string requiredResourceName in requiredResourceNames.OrderBy(x => x))
		{
			if (!actualKeysInResourceFile.Contains(requiredResourceName))
				WriteError(ErrorFactory.CreateErrorMessageResourceNameNotFoundError(errorMessageResourceType, requiredResourceName));
		}
	}


	private void WriteManifestFile(HashSet<string> requiredResourceNames)
	{
		var builder = new StringBuilder();
		builder.AppendLine("ErrorMessageResourceName");
		foreach (string requiredResourceName in requiredResourceNames)
			builder.AppendLine(requiredResourceName);
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

		public ValidationAttributeInfo(CustomAttribute validationAttribute, string? errorMessageResourceName)
		{
			ValidationAttribute = validationAttribute ?? throw new ArgumentNullException(nameof(validationAttribute));
			ErrorMessageResourceName = errorMessageResourceName;
		}
	}
}
