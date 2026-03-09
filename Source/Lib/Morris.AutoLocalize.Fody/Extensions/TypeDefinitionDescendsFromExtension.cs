using Mono.Cecil;
using System.Linq;

namespace Morris.AutoLocalize.Fody.Extensions;

internal static class TypeDefinitionDescendsFromExtension
{
	public static bool DescendsFrom(this TypeDefinition child, TypeReference baseType)
	{
		if (child.IsClass)
		{
			TypeReference? current = child.BaseType;
			while (current is not null)
			{
				if (current.IsSameAs(baseType))
					return true;

				current = current.Resolve()?.BaseType;
			}
		}
		else if (child.IsInterface)
		{
			return ImplementsInterface(child, baseType);
		}

		return false;
	}

	private static bool ImplementsInterface(TypeDefinition child, TypeReference baseType)
	{
		foreach (InterfaceImplementation item in child.Interfaces)
		{
			TypeReference interfaceType = item.InterfaceType;
			if (interfaceType.IsSameAs(baseType))
				return true;

			TypeDefinition? resolved = interfaceType.Resolve();
			if (resolved is not null && ImplementsInterface(resolved, baseType))
				return true;
		}

		return false;
	}
}
