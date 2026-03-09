using Mono.Cecil;
using System.Collections.Generic;

namespace Morris.AutoLocalize.Fody;

internal class AutoLocalizeValidationAttributesAttributeData
{
	public TypeReference ErrorMessageResourceType { get; }
	public string ErrorMessageResourceNamePrefix { get; }

	public AutoLocalizeValidationAttributesAttributeData(
		TypeReference errorMessageResourceType,
		string errorMessageResourceNamePrefix)
	{
		ErrorMessageResourceType = errorMessageResourceType;
		ErrorMessageResourceNamePrefix = errorMessageResourceNamePrefix;
	}

	public static AutoLocalizeValidationAttributesAttributeData FromDictionary(Dictionary<string, object?> values)
	{
		var errorMessageResourceType =
			(TypeReference)values[nameof(AutoLocalizeValidationAttributesAttribute.ErrorMessageResourceType)]!;

		string errorMessageResourceNamePrefix = "Validation_";
		if (values.TryGetValue(nameof(AutoLocalizeValidationAttributesAttribute.ErrorMessageResourceNamePrefix), out object? val) && val is not null)
		{
			errorMessageResourceNamePrefix = (string)val!;
		}

		return new AutoLocalizeValidationAttributesAttributeData(
			errorMessageResourceType: errorMessageResourceType,
			errorMessageResourceNamePrefix: errorMessageResourceNamePrefix);
	}
}
