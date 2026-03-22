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
		Fody.TestResult testResult,
		IEnumerable<string>? requiredManifestEntries = null,
		IEnumerable<string>? requiredErrorMessages = null,
		string resourceTypeName = "UnitTest.AppStrings",
		string resourceNamePrefix = "AutoLocalize_")
	{
		requiredErrorMessages ??= [];
		var discoveredResourceNamesHashSet = new HashSet<string>();
		Type resourceType = testResult.Assembly.GetType(resourceTypeName)!;
		Assert.True(
			resourceType is not null,
			$"Resource type \"{resourceTypeName}\" not found in assembly."
		);

		foreach (Type type in testResult.Assembly.GetTypes())
		{
			ScanType(
				type,
				resourceType,
				resourceNamePrefix,
				discoveredResourceNamesHashSet
			);
		}

		if (requiredManifestEntries is not null)
		{
			requiredManifestEntries
				.GetDifferences(
					discoveredResourceNamesHashSet,
					additionalItems: out string[] unexpectedResourceNames,
					missingItems: out string[] missingResourceNames
				);

			if (unexpectedResourceNames.Any() || missingResourceNames.Any())
			{
				var builder = new StringBuilder();
				builder.AddItems("Missing resource names", missingResourceNames);
				builder.AddItems("Unexpected resource names", unexpectedResourceNames);
				Assert.Fail($"There were discrepancies in the registered resource names{Environment.NewLine}{builder}");
			}
		}

		requiredErrorMessages.
			GetDifferences(
				comparison: testResult.Errors.Select(x => "Error: " + x.Text).Union(testResult.Warnings.Select(x => "Warning: " + x.Text)),
				out string[] unexpectedErrorMessages,
				out string[] missingErrorMessages
			);

		if (unexpectedErrorMessages.Any() || missingErrorMessages.Any())
		{
			var builder = new StringBuilder();
			builder.AddItems("The following weaver errors were expected but not found", missingErrorMessages);
			builder.AddItems("The following unexpected weaver errors were uncountered", unexpectedErrorMessages);
			Assert.Fail($"There were discrepancies in the weaver errors output{Environment.NewLine}{builder}");
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
