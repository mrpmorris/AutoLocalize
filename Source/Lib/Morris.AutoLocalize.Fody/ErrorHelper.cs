using Mono.Cecil;

namespace Morris.AutoLocalize.Fody;

internal static class ErrorFactory
{
	public const string ErrorMessageResourceNameNotFoundCode = "MAL0001";

	public static string CreateErrorMessageResourceNameNotFoundError(TypeReference errorMessageResourceType, string errorMessageResourceName) =>
		$"{ErrorMessageResourceNameNotFoundCode}: ErrorMessageResourceName \"{errorMessageResourceName}\""
		+ $" not found in resource \"{errorMessageResourceType.FullName}\".";
}
