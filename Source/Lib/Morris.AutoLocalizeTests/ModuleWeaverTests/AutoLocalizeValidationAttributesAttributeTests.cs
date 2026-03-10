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

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedResourceNames: ["AutoLocalize_Required"]
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
			expectedResourceNames: ["AutoLocalize_Required"]
		);
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

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedResourceNames: ["Bob"]
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

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedResourceNames: ["AutoLocalize_Required"]
		);

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

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedResourceNames: []
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

		WeaverExecutor.Execute(sourceCode, out Fody.TestResult? fodyTestResult, out string? manifest);

		AssemblyHelper.AssertWeaverResults(
			fodyTestResult.Assembly,
			expectedResourceNames: []
		);
	}

}

