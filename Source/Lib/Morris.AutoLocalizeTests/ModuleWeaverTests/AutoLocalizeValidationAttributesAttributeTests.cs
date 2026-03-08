using Morris.AutoLocalizeTests.Helpers;

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

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedNumberOfAffectedAttributes: 1,
			expectedResourceNames: ["Validation_Required"]
		);
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

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedNumberOfAffectedAttributes: 1,
			expectedResourceNames: ["Validation_Required"]
		);
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

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedNumberOfAffectedAttributes: 0,
			expectedResourceNames: []
		);
	}


	[Fact]
	public void WhenErrorMessageResourceNameIsAlreadySet_ThenNoActionIsTaken()
	{
		string sourceCode =
			"""
			using Morris.AutoLocalize;
			using System.ComponentModel.DataAnnotations;

			[assembly:AutoLocalizeValidationAttributes(typeof(UnitTest.AppStrings))]

			namespace UnitTest;

			public class Person
			{
				[Required(ErrorMessageResourceName="Test")]
				public string Name { get; set; }
			}
			""";

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedNumberOfAffectedAttributes: 0,
			expectedResourceNames: []
		);
	}

}

