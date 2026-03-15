using Morris.AutoLocalize.Fody.Helpers;
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
		IEnumerable<string>? expectedManifestEntries = null,
		string resourceTypeName = "UnitTest.AppStrings",
		string resourceNamePrefix = "AutoLocalize_")
	{
		var discoveredResourceNamesHashSet = new HashSet<string>();
		Type resourceType = assembly.GetType(resourceTypeName)!;
		Assert.True(
			resourceType is not null,
			$"Resource type \"{resourceTypeName}\" not found in assembly."
		);

		foreach (Type type in assembly.GetTypes())
		{
			ScanType(
				type,
				resourceType,
				resourceNamePrefix,
				discoveredResourceNamesHashSet
			);
		}

		if (expectedManifestEntries is not null)
		{
			expectedManifestEntries
				.GetDifferences(
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
		}

		static void AddItems(StringBuilder builder, string title, IEnumerable<string> items)
		{
			if (!items.Any())
				return;

			string lineSeparator = new string('=', title.Length);
			builder.AppendLine(lineSeparator);
			builder.AppendLine(title);
			builder.AppendLine(lineSeparator);
			foreach (string item in items)
			{
				builder.AppendLine($"  - {item}");
			}
		}
	}

	private static void ScanType(
		Type type,
		Type resourceType,
		string resourceNamePrefix,
		HashSet<string> discoveredResourceNames)
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

		foreach (MemberInfo memberInfo in memberInfos)
		{
			ScanMember(
				memberInfo,
				resourceType,
				resourceNamePrefix,
				discoveredResourceNames);
		}
	}

	private static void ScanMember(
		MemberInfo memberInfo,
		Type resourceType,
		string resourceNamePrefix,
		HashSet<string> discoveredResourceName)
	{
		IEnumerable<ValidationAttribute> validationAttributes = memberInfo
			.GetCustomAttributes()
			.OfType<ValidationAttribute>();

		foreach (ValidationAttribute validationAttribute in validationAttributes)
		{
			if (validationAttribute.ErrorMessageResourceName is not null)
			{
				string name = StringHelper.GetAttributeShortName(validationAttribute.GetType().Name);
				discoveredResourceName.Add(validationAttribute.ErrorMessageResourceName!);
			}
		}
	}
}
