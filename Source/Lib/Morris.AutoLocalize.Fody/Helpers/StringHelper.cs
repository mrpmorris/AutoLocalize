using System;

namespace Morris.AutoLocalize.Fody.Helpers;

public static class StringHelper
{
	public static string GetAttributeShortName(string attributeTypeName) =>
		(
			attributeTypeName.EndsWith("Attribute", StringComparison.Ordinal)
			? attributeTypeName.Substring(0, attributeTypeName.Length - 9)
			: attributeTypeName
		)
		.Split('`')[0];
}
