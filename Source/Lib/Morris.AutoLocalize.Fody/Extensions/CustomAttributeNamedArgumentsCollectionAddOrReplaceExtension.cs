using Mono.Cecil;
using Mono.Collections.Generic;
using System.Collections.Generic;
using System.Linq;

namespace Morris.AutoLocalize.Fody.Extensions;

internal static class CustomAttributeNamedArgumentsCollectionAddOrReplaceExtension
{
	public static void AddOrReplace(this Collection<CustomAttributeNamedArgument> arguments, CustomAttributeNamedArgument arg)
	{
		Dictionary<string, int> argumentsByName =
			arguments
			.Select((x, index) => (x, index))
			.ToDictionary(x => x.x.Name, x => x.index);

		if (argumentsByName.TryGetValue(arg.Name, out int index))
			arguments.RemoveAt(index);
		arguments.Add(arg);
	}
}
