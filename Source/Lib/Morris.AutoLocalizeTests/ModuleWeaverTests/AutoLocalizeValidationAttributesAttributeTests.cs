using Morris.AutoLocalizeTests.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Morris.AutoLocalizeTests.ModuleWeaverTests;

public class AutoLocalizeValidationAttributesAttributeTests
{
	[Fact]
	public void WhenErrorMessageResourceTypeAndErrorMessageResourceNameAreNotSetOnPropertyValidationAttribute_ThenTheyAreSet()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[Required]
				public string Name { get; set; }
			}
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest);

		AssemblyHelper.AssertWeaverResults(fodyTestResult.Assembly);
	}

	[Fact]
	public void WhenErrorMessageResourceTypeAndErrorMessageResourceNameAreNotSetOnFieldValidationAttribute_ThenTheyAreSet()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[Required]
				public string Name;
			}
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest);

		AssemblyHelper.AssertWeaverResults(fodyTestResult.Assembly);
	}


	[Fact]
	public void WhenErrorMessageNameIsSetOnPropertyValidationAttribute_ThenItIsPreserved_AndErrorMessageResourceTypeIsUpdated()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[Required(ErrorMessageResourceType=null, ErrorMessageResourceName = "Bob")]
				public string Name { get; set; }
			}
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest,
			assemblyResourceValues: [new("Bob", null)]);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedManifestEntries: ["Bob"]
		);

		Type? person = fodyTestResult.Assembly.GetType("UnitTest.Person");
		Assert.NotNull(person);

		PropertyInfo? nameProperty = person.GetProperty("Name");
		Assert.NotNull(nameProperty);

		var requiredAttribute = nameProperty.GetCustomAttribute<RequiredAttribute>();
		Assert.NotNull(requiredAttribute);

		Assert.Equal("Bob", requiredAttribute.ErrorMessageResourceName);
		Assert.Equal("UnitTest.AppStrings", requiredAttribute.ErrorMessageResourceType?.FullName);

	}


	[Fact]
	public void WhenAttributeIsUpdated_ThenItIsAccessibleViaReflection()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[Required]
				public string Name { get; set; }
			}
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest,
			assemblyResourceValues: [new("AutoLocalize_Required", null)]);

		AssemblyHelper.AssertWeaverResults(fodyTestResult.Assembly);

		Type? person = fodyTestResult.Assembly.GetType("UnitTest.Person");
		Assert.NotNull(person);

		PropertyInfo? nameProperty = person.GetProperty("Name");
		Assert.NotNull(nameProperty);

		var requiredAttribute = nameProperty.GetCustomAttribute<RequiredAttribute>();
		Assert.NotNull(requiredAttribute);

		Assert.Equal("AutoLocalize_Required", requiredAttribute.ErrorMessageResourceName);
		Assert.Equal("UnitTest.AppStrings", requiredAttribute.ErrorMessageResourceType?.FullName);
	}

	[Fact]
	public void WhenErrorMessageResourceTypeIsAlreadySet_ThenNoActionIsTaken()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[Required(ErrorMessageResourceType=typeof(UnitTest.AppStrings))]
				public string Name { get; set; }
			}
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedManifestEntries: []
		);
	}

	[Fact]
	public void WhenErrorMessageIsSet_ThenNoActionIsTaken()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[Required(ErrorMessage="You must enter a name.")]
				public string Name { get; set; }
			}
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest,
			assemblyResourceValues: [new("AutoLocalize_Required", null)]);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedManifestEntries: []
		);
	}

	[Fact]
	public void WhenErrorMessageResourceNameIsInResourcesFile_NoErrorIsOutput()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[StringLength(50)]
				public string Name { get; set; }
			}
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest,
			assemblyResourceValues: [new("AutoLocalize_StringLength", null)]);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedManifestEntries: ["AutoLocalize_Required"]
		);
	}

	[Fact]
	public void WhenErrorMessageResourceNameIsNotInResourcesFile_ThenOutputsErrorMessage()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[StringLength(50)]
				public string Name { get; set; }
			}
			""";

		WeaverExecutor.Execute(
			sourceCode: sourceCode,
			testResult: out Fody.TestResult? fodyTestResult,
			manifest: out string? manifest);

		AssemblyHelper.AssertWeaverResults(fodyTestResult.Assembly);
	}

}

