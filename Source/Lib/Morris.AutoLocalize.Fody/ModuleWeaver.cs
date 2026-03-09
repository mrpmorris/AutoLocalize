using Fody;
using Mono.Cecil;
using Morris.AutoLocalize.Fody.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		return AutoLocalizeValidationAttributesAttributeData.FromDictionary(values);
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

		var addedResourceNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (TypeDefinition type in classesToScan)
			ScanType(validationAttributeType, addedResourceNames, attributeData, type);

		var manifestBuilder = new StringBuilder();
		IEnumerable<string> addedResourceNamesOrdered =
			addedResourceNames
			.OrderBy(x => x);
		manifestBuilder.AppendLine("ErrorMessageResourceName");
		foreach (string addedResourceName in addedResourceNamesOrdered)
			manifestBuilder.AppendLine(addedResourceName);
		WriteManifestFile(manifestBuilder.ToString());
	}

	private void ScanType(
		TypeDefinition validationAttributeType,
		HashSet<string> addedResourceNames,
		AutoLocalizeValidationAttributesAttributeData attributeData,
		TypeDefinition type)
	{
		IEnumerable<IMemberDefinition> memberDefinitions = [.. type.Fields, .. type.Properties];
		memberDefinitions = memberDefinitions.Where(x => !x.Name.StartsWith("<"));
		foreach (IMemberDefinition memberDefinition in memberDefinitions)
			ScanMember(
				validationAttributeType,
				addedResourceNames,
				attributeData,
				memberDefinition
			);
	}

	private void ScanMember(
		TypeDefinition validationAttributeType,
		HashSet<string> addedResourceNames,
		AutoLocalizeValidationAttributesAttributeData attributeData,
		IMemberDefinition memberDefinition)
	{
		IEnumerable<CustomAttribute> validationAttributes =
			memberDefinition
			.CustomAttributes
			.Select(x => new
			{
				TypeDefinition = x.AttributeType.Resolve(),
				CustomAttribute = x
			}
			)
			.Where(x => x.TypeDefinition.IsAssignableTo(validationAttributeType))
			.Where(x =>
				{
					Dictionary<string, object?> values = x.CustomAttribute.GetValues();
					values.TryGetValue("ErrorMessageResourceType", out object? errorMessageResourceType);
					values.TryGetValue("ErrorMessageResourceName", out object? errorMessageResourceName);

					return errorMessageResourceType is null
						&& errorMessageResourceName is null;
				}
			)
			.Select(x => x.CustomAttribute);

		foreach (CustomAttribute attribute in validationAttributes)
			UpdateValidationAttribute(attributeData, addedResourceNames, attribute);
	}

	private void UpdateValidationAttribute(
		AutoLocalizeValidationAttributesAttributeData attributeData,
		HashSet<string> addedResourceNames,
		CustomAttribute attribute)
	{
		string attributeTypeName = attribute.AttributeType.Name;

		string resourceKeySuffix =
			attributeTypeName.EndsWith("Attribute", StringComparison.Ordinal)
			? attributeTypeName.Substring(0, attributeTypeName.Length - 9)
			: attributeTypeName;

		string resourceName = attributeData.ErrorMessageResourceNamePrefix + resourceKeySuffix;

		TypeReference systemTypeReference = FindTypeDefinition("System.Type");

		attribute.Properties.Add(
			new CustomAttributeNamedArgument(
				"ErrorMessageResourceType",
				new CustomAttributeArgument(
					type: systemTypeReference,
					value: attributeData.ErrorMessageResourceType
				)
			)
		);

		attribute.Properties.Add(
			new CustomAttributeNamedArgument(
				"ErrorMessageResourceName",
				new CustomAttributeArgument(
					type: ModuleDefinition.TypeSystem.String,
					value: resourceName
				)
			)
		);

		addedResourceNames.Add(resourceName);
	}

	private void WriteManifestFile(string content)
	{
		string manifestFilePath = Path.ChangeExtension(ProjectFilePath, "Morris.AutoLocalize.csv");
		File.WriteAllText(manifestFilePath, content);
	}

	private void RemoveDependency()
	{
		var assemblyReference =
			ModuleDefinition
			.AssemblyReferences
			.FirstOrDefault(x => x.Name == "Morris.AutoLocalize");

		if (assemblyReference is not null)
			ModuleDefinition.AssemblyReferences.Remove(assemblyReference);
	}
}
