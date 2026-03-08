namespace Morris.AutoLocalizeTests.Extensions;

internal static class EnumerableExtensions
{
	public static void GetDifferences<T>(
		this IEnumerable<T> source,
		IEnumerable<T> comparison,
		out T[] additionalItems,
		out T[] missingItems)
	{
		additionalItems = comparison.Except(source).ToArray();
		missingItems = source.Except(comparison).ToArray();
	}
}
