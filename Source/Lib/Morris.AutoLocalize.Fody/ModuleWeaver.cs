using Fody;
using Mono.Cecil;
using Morris.AutoLocalize.Fody.Extensions;
using Morris.AutoLocalize.Fody.Helpers;
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
		WriteDebug("Executing " + GetType().FullName);
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
		WriteDebug($"Finding {nameof(AutoLocalizeValidationAttributesAttribute)} on assembly {Path.GetFileName(AssemblyFilePath)}");
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

		WriteDebug("Attribute found");
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
		WriteDebug("Processing classes");
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
		WriteDebug($"Scanning class {type.FullName}");
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
		WriteDebug($"    Scanning member {memberDefinition.Name}");
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

		string resourceKeySuffix = StringHelper.GetAttributeShortName(attributeTypeName);
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
		WriteDebug($"        Updated {attribute.AttributeType.Name} with"
			+ $" ErrorMessageResourceType={systemTypeReference.FullName},"
			+ $" ErrorMessageResourceName={resourceName}");
	}

	private void WriteManifestFile(string content)
	{
		WriteDebug("Writing manifest file");
		string manifestFilePath = Path.ChangeExtension(ProjectFilePath, "Morris.AutoLocalize.csv");
		File.WriteAllText(manifestFilePath, content);
	}

	private void RemoveDependency()
	{
		WriteDebug("Removing assembly references");
		AssemblyNameReference? assemblyReference =
			ModuleDefinition
			.AssemblyReferences
			.FirstOrDefault(x => x.Name == "Morris.AutoLocalize");

		if (assemblyReference is not null)
			ModuleDefinition.AssemblyReferences.Remove(assemblyReference);
	}
}
