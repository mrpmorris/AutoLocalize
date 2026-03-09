using Mono.Cecil;

namespace Morris.AutoLocalize.Fody.Extensions;

internal static class TypeDefinitionIsAssignableToExtension
{
	public static bool IsAssignableTo(this TypeReference candidate, TypeReference target) =>
		candidate.IsSameAs(target)
		|| candidate.Resolve().DescendsFrom(target);
}
