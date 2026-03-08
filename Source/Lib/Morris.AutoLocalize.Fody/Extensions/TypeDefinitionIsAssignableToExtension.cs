using Mono.Cecil;

namespace Morris.AutoLocalize.Fody.Extensions;

internal static class TypeDefinitionIsAssignableToExtension
{
	public static bool IsAssignableTo(this TypeDefinition candidate, TypeDefinition target) =>
		candidate == target.Resolve()
		|| candidate.DescendsFrom(target);
}
