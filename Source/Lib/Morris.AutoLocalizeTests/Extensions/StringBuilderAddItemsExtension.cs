using System.Text;

namespace Morris.AutoLocalizeTests.Extensions;

internal static class StringBuilderAddItemsExtension
{
	public static void AddItems(this StringBuilder builder, string title, IEnumerable<string> items)
	{
		if (!items.Any())
			return;

		string lineSeparator = new string('=', title.Length);
		builder.AppendLine();
		builder.AppendLine(title);
		builder.AppendLine(lineSeparator);
		foreach (string item in items)
			builder.AppendLine($"  - {item}");
		builder.AppendLine();
	}
}
