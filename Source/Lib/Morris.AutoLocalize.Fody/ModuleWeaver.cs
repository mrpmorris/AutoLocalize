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

		var requiredResourceNames = new Dictionary<string, SequencePoint?>(StringComparer.Ordinal);
		foreach (TypeDefinition type in classesToScan)
			ScanType(validationAttributeType, requiredResourceNames, attributeData, type);

		EnsureRequiredResourceNamesExistInResourceFile(attributeData.ErrorMessageResourceType, requiredResourceNames);
		WriteManifestFile(requiredResourceNames);
	}

	private void ScanType(
		TypeDefinition validationAttributeType,
		Dictionary<string, SequencePoint?> requiredResourceNames,
		AutoLocalizeValidationAttributesAttributeData attributeData,
		TypeDefinition type)
	{
		IEnumerable<IMemberDefinition> memberDefinitions = [.. type.Fields, .. type.Properties];
		memberDefinitions = memberDefinitions.Where(x => !x.Name.StartsWith("<"));
		foreach (IMemberDefinition memberDefinition in memberDefinitions)
			ScanMember(
				validationAttributeType,
				requiredResourceNames,
				attributeData,
				memberDefinition
			);
	}

	private void ScanMember(
		TypeDefinition validationAttributeType,
		Dictionary<string, SequencePoint?> requiredResourceNames,
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
				(!x.AttributeValues.TryGetValue("ErrorMessageResourceType", out object? resourceType) || resourceType is null)
				&&
				(!x.AttributeValues.TryGetValue("ErrorMessage", out object? errorMessage) || errorMessage is null)
			)
			.Select(x =>
				new ValidationAttributeInfo(
					x.ValidationAttribute,
					x.AttributeValues.TryGetValue("ErrorMessageResourceName", out object? value) ? (string?)value : null
				)
			);

		foreach (ValidationAttributeInfo validationAttribute in validationAttributes)
			UpdateValidationAttribute(attributeData, requiredResourceNames, validationAttribute, memberDefinition);
	}

	private void UpdateValidationAttribute(
		AutoLocalizeValidationAttributesAttributeData attributeData,
		Dictionary<string, SequencePoint?> requiredResourceNames,
		ValidationAttributeInfo validationAttributeInfo,
		IMemberDefinition memberDefinition)
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

		SequencePoint? sequencePoint = GetSequencePoint(memberDefinition);

		if (!string.IsNullOrEmpty(validationAttributeInfo.ErrorMessageResourceName))
		{
			if (!requiredResourceNames.ContainsKey(validationAttributeInfo.ErrorMessageResourceName!))
				requiredResourceNames.Add(validationAttributeInfo.ErrorMessageResourceName!, sequencePoint);
		}
		else
		{
			if (!requiredResourceNames.ContainsKey(resourceName))
				requiredResourceNames.Add(resourceName, sequencePoint);
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
		Dictionary<string, SequencePoint?> requiredResourceNames)
	{
		HashSet<string> actualKeysInResourceFile = GetActualResourceNamesForResourceType(errorMessageResourceType);
		foreach (KeyValuePair<string, SequencePoint?> kvp in requiredResourceNames.OrderBy(x => x.Key))
		{
			if (!actualKeysInResourceFile.Contains(kvp.Key))
			{
				string message = ErrorFactory.CreateErrorMessageResourceNameNotFoundError(errorMessageResourceType, kvp.Key);
				if (kvp.Value is not null)
					WriteError(message, kvp.Value);
				else
					WriteError(message);
			}
		}
	}


	private void WriteManifestFile(Dictionary<string, SequencePoint?> requiredResourceNames)
	{
		var builder = new StringBuilder();
		builder.AppendLine("ErrorMessageResourceName");
		foreach (string requiredResourceName in requiredResourceNames.Keys)
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

	private static SequencePoint? GetSequencePoint(IMemberDefinition memberDefinition)
	{
		MethodDefinition? method = null;
		if (memberDefinition is PropertyDefinition property)
			method = property.GetMethod ?? property.SetMethod;

		return method?.DebugInformation?.SequencePoints?.FirstOrDefault();
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
