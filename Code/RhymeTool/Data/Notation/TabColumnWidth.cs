
namespace Skinnix.RhymeTool.Data.Notation;

public readonly record struct TabColumnWidth(int Min, int Max)
{
	public static TabColumnWidth One { get; } = new(1, 1);

	public static TabColumnWidth Calculate(IEnumerable<string> strings)
	{
		var min = int.MaxValue;
		var max = 0;
		foreach (var s in strings)
		{
			if (s.Length < min)
				min = s.Length;
			if (s.Length > max)
				max = s.Length;
		}

		if (min == int.MaxValue)
			min = max;

		return new(min, max);
	}

	public static string[] Calculate(IEnumerable<string> strings, out TabColumnWidth width)
	{
		var min = int.MaxValue;
		var max = 0;
		var result = strings
			.Select(n =>
			{
				var noteString = n.ToString();

				if (noteString.Length < min)
					min = noteString.Length;
				if (noteString.Length > max)
					max = noteString.Length;

				return noteString;
			})
			.ToArray();

		if (min == int.MaxValue)
			min = max;

		width = new(min, max);
		return result;
	}
}
