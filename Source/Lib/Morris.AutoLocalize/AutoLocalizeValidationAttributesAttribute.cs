using System;
#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Morris.AutoLocalize;

/// <summary>
/// Scans the assembly and registers all dependencies that match
/// the given criteria.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
#if PublicContracts
public
#else
internal
#endif
class AutoLocalizeValidationAttributesAttribute : Attribute
{
	/// <summary>
	/// Specifies the type of the resource in which to expect error messages
	/// </summary>
	public Type ErrorMessageResourceType { get; set; }


	/// <summary>
	/// Specifies the prefix to use for the key of the resource. For example if
	/// the value is "AutoLocalize_" and the ValidationAttribute used is 
	/// RequiredAttribute then the expected key would be "AutoLocalize_Required"
	/// </summary>
	public string ErrorMessageResourceNamePrefix { get; set; } = "AutoLocalize_";

	/// <summary>
	/// Updates ErrorMessageResourceType and ErrorMessageResourceName on ValidationAttribute descendants defined in the specified assembly.
	/// </summary>
	public AutoLocalizeValidationAttributesAttribute(Type errorMessageResourceType)
	{
		ErrorMessageResourceType = errorMessageResourceType ?? throw new ArgumentNullException(nameof(errorMessageResourceType));
	}
}
