using Microsoft.CodeAnalysis;
using Morris.AutoLocalize;
using System.Reflection;

namespace Morris.AutoLocalizeTests.ModuleWeaverTests;

public class RemoveAutoLocalizeDependencyTests
{
	[Fact]
	public void WhenWeavingIsSuccessful_ThenReferenceToAutoLocalizeShouldHaveBeenRemoved()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest,
			assemblyResourceValuesToCreate: []);

		bool isReferenced =
			fodyTestResult
			.Assembly
			.GetReferencedAssemblies()
			.Select(x => x.FullName.Split(',')[0])
			.Any(x => x == "Morris.AutoLocalize");
		Assert.False(isReferenced, "Morris.AutoLocalize should not be referenced.");
	}

	[Fact]
	public void WhenWeavingIsSuccessful_ThenAutoLocalizeAttributesShouldHaveBeenRemoved()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest,
			assemblyResourceValuesToCreate: [new("AutoLocalize_Required", null)]);

		IEnumerable<Attribute> attributes = fodyTestResult
			.Assembly
			.GetCustomAttributes()
			.Where(x => x.GetType().FullName == typeof(AutoLocalizeValidationAttributesAttribute).FullName);

		Assert.Empty(attributes);
	}

	[Fact]
	public void WhenCantFindAutoLocalizeValidationAttributesAttribute_ThenReferenceToAutoLocalizeShouldHaveBeenRemoved()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			//[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest,
			assemblyResourceValuesToCreate: []);

		bool isReferenced =
			fodyTestResult
			.Assembly
			.GetReferencedAssemblies()
			.Select(x => x.FullName.Split(',')[0])
			.Any(x => x == "Morris.AutoLocalize");
		Assert.False(isReferenced, "Morris.AutoLocalize should not be referenced.");
	}

}
