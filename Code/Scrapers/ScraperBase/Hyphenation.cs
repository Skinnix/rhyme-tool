namespace ScraperBase;

public record Hyphenation(byte[] Positions)
{
	public Hyphenation(IReadOnlyCollection<byte> Positions)
		: this([.. Positions])
	{ }

	public IEnumerable<string> GetSyllables(string text)
	{
		var previous = 0;
		foreach (var position in Positions)
		{
			var syllable = text[previous..position];
			yield return syllable;
			previous = position;
		}

		var lastSyllable = text[previous..];
		yield return lastSyllable;
	}
}
