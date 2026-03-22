using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;

namespace Morris.AutoLocalize.Fody.Extensions;

internal static class IMemberGetSequencePointExtension
{
	public static SequencePoint? GetSequencePoint(this IMemberDefinition member)
	{
		if (member is MethodDefinition method)
			return method
				.DebugInformation
				?.SequencePoints
				?.FirstOrDefault(sp => !sp.IsHidden);

		if (member is PropertyDefinition prop)
		{
			MethodDefinition accessor = prop.GetMethod ?? prop.SetMethod;
			return accessor
				?.DebugInformation
				?.SequencePoints
				?.FirstOrDefault(sp => !sp.IsHidden);
		}

		if (member is FieldDefinition field)
			return field.DeclaringType.GetSequencePoint();

		if (member is TypeDefinition type)
			return type
				.Methods
				.SelectMany(m => m.DebugInformation?.SequencePoints ?? [])
				.FirstOrDefault(sp => !sp.IsHidden);

		return null;
	}
}
