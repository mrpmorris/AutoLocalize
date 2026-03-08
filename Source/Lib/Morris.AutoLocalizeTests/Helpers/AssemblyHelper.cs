using Morris.AutoLocalizeTests.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace Morris.AutoLocalizeTests.Helpers;

internal class AssemblyHelper
{
	private const BindingFlags DefaultBindingFlags = 
		BindingFlags.Instance
		| BindingFlags.Static
		| BindingFlags.Public
		| BindingFlags.NonPublic;

	public static void AssertWeaverResults(
		Assembly assembly,
		int expectedNumberOfAffectedAttributes,
		IEnumerable<string> expectedResourceNames,
		string resourceTypeName = "UnitTest.AppStrings",
		string resourceNamePrefix = "Validation_")
	{
		var discoveredResourceNamesHashSet = new HashSet<string>();
		Type resourceType = assembly.GetType(resourceTypeName)!;
		Assert.True(
			resourceType is not null,
			$"Resource type \"{resourceTypeName}\" not found in assembly."
		);

		int actualNumberOfAffectedAttributes = 0;
		foreach (var type in assembly.GetTypes())
		{
			ScanType(
				type,
				resourceType,
				resourceNamePrefix,
				discoveredResourceNamesHashSet,
				out int typeActualNumberOfAffectedAttributes
			);
			actualNumberOfAffectedAttributes += typeActualNumberOfAffectedAttributes;
		}

		Assert.True(
			actualNumberOfAffectedAttributes == expectedNumberOfAffectedAttributes,
			$"Expected to affect {expectedNumberOfAffectedAttributes} classes but found {actualNumberOfAffectedAttributes}."
		);

		expectedResourceNames.GetDifferences(
			discoveredResourceNamesHashSet,
			additionalItems: out string[] unexpectedResourceNames,
			missingItems: out string[] missingResourceNames
		);

		if (unexpectedResourceNames.Any() || missingResourceNames.Any())
		{
			var builder = new StringBuilder();
			AddItems(builder, "Missing resource names", missingResourceNames);
			AddItems(builder, "Unexpected resource names", unexpectedResourceNames);
			Assert.Fail($"There were discrepancies in the registered resource names{Environment.NewLine}{builder}");
		}

		static void AddItems(StringBuilder builder, string title, IEnumerable<string> items)
		{
			if (!items.Any())
				return;

			string lineSeparator = new string('=', title.Length);
			builder.AppendLine(lineSeparator);
			builder.AppendLine(title);
			builder.AppendLine(lineSeparator);
			foreach(string item in items)
			{
				builder.AppendLine($"  - {item}");
			}
		}
	}

	private static void ScanType(
		Type type,
		Type resourceType,
		string resourceNamePrefix,
		HashSet<string> discoveredResourceNames,
		out int typeActualNumberOfAffectedAttributes)
	{
		IEnumerable<MemberInfo> memberInfos =
			type
			.GetProperties(DefaultBindingFlags)
			.OfType<MemberInfo>()
			.Union(
				type
				.GetFields(DefaultBindingFlags)
				.OfType<MemberInfo>()
			)
			.Where(x => !x.Name.StartsWith("<"));

		typeActualNumberOfAffectedAttributes = 0;
		foreach (MemberInfo memberInfo in memberInfos)
		{
			ScanMember(
				memberInfo,
				resourceType,
				resourceNamePrefix,
				discoveredResourceNames,
				out int memberActualNumberOfAffectedAttributes);
			typeActualNumberOfAffectedAttributes += memberActualNumberOfAffectedAttributes;
		}
	}

	private static void ScanMember(
		MemberInfo memberInfo,
		Type resourceType,
		string resourceNamePrefix,
		HashSet<string> discoveredResourceName,
		out int memberActualNumberOfAffectedAttributes)
	{
		IEnumerable<ValidationAttribute> validationAttributes = memberInfo
			.GetCustomAttributes()
			.OfType<ValidationAttribute>();

		memberActualNumberOfAffectedAttributes = 0;
		foreach(ValidationAttribute validationAttribute in validationAttributes)
		{
			string name = validationAttribute.GetType().Name;
			if (name.EndsWith("Attribute", StringComparison.Ordinal))
				name = name.Substring(0, name.Length - 9);

			string resourceName = $"{resourceNamePrefix}{name}";
			if (validationAttribute.ErrorMessageResourceType == resourceType
				&& validationAttribute.ErrorMessageResourceName == resourceName)
			{
				memberActualNumberOfAffectedAttributes++;
				discoveredResourceName.Add(resourceName);
			}
		}
	}
}
