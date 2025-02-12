using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Client.IO;

public partial class CpsFile : SongFile
{
	#region Directives
	public const string TAB_DIRECTIVE = "tab";
	public const string PART_DIRECTIVE = "part";
	#endregion

	[GeneratedRegex(@"^(?<Sep>[^A-Za-z0-9\s])(?<DirS>[^A-Za-z0-9\s])CPSong(?<AttS>[^A-Za-z0-9\s])(?<Ver>\d+)(?<AttE>[^A-Za-z0-9\s])(?<DirE>[^A-Za-z0-9\s])(?<Esc>[^A-Za-z0-9\s])$")]
	private static partial Regex GenerateHeaderRegex();
	private static readonly Regex headerRegex = GenerateHeaderRegex();

	public event SongFileErrorHandler? Error;

	public CpsFile(Configuration configuration)
		: base(configuration)
	{ }

	public CpsFile()
		: this(Configuration.Default)
	{ }

	public static CpsFile? TryCreate(string header)
	{
		var match = headerRegex.Match(header);
		if (!match.Success)
			return null;

		var version = int.Parse(match.Groups["Ver"].Value);
		var configuration = new Configuration(
			match.Groups["DirS"].Value[0],
			match.Groups["DirE"].Value[0],
			match.Groups["Sep"].Value[0],
			match.Groups["Esc"].Value[0],
			match.Groups["AttS"].Value[0],
			match.Groups["AttE"].Value[0]
		);
		if (!configuration.IsValid)
			return null;

		return new CpsFile(configuration);
	}

	public string CreateHeader(int version)
	{
		return $"{TokenConfiguration.SeparatorChar}{TokenConfiguration.DirectiveStartChar}CPSong{TokenConfiguration.AttachmentStartChar}{version}{TokenConfiguration.AttachmentEndChar}{TokenConfiguration.DirectiveEndChar}{TokenConfiguration.EscapeChar}";
	}

	public SongLine? TryParseLine(List<RangeToken> currentTokens, string line)
	{
		var lexed = LexChars(line);
		var combined = CombineText(lexed);
		if (!CombineLines(currentTokens, combined))
			return null;

		return ParseLine(currentTokens, (type, token, expected) => Error?.Invoke(type, token, expected));
	}

	public SongLine ParseLine(List<RangeToken> currentTokens)
		=> ParseLine(currentTokens, (type, token, expected) => Error?.Invoke(type, token, expected));

	public IEnumerable<SongLine> Parse(IEnumerable<string> lines)
	{
		var enumerator = lines.GetEnumerator();
		List<RangeToken>? currentLine = null;
		while (enumerator.MoveNext())
		{
			var line = enumerator.Current;
			var lexed = LexChars(line);
			var combined = CombineText(lexed);
			if (!CombineLines(currentLine ??= new(), combined))
				continue;

			yield return ParseLine(currentLine, HandleError);
			currentLine = null;
		}

		if (currentLine is not null)
		{
			HandleError(ErrorType.UnexpectedEnd, default, null);
			yield return ParseLine(currentLine, HandleError);
		}
	}

	public string Stringify(IEnumerable<SongLine> lines)
	{
		var sb = new StringBuilder(CreateHeader(1));
		sb.AppendLine();
		foreach (var line in lines)
		{
			line.Write(sb, TokenConfiguration);
			sb.AppendLine();
		}

		return sb.ToString();
	}

	private void HandleError(ErrorType type, RangeToken token, TokenType? expected)
	{
		if (expected is null)
			Console.WriteLine($"Error: {type} at {token.Type} ({token.Value.Index})");
		else
			Console.WriteLine($"Error: {type} at {token.Type} ({token.Value.Index}), {expected} expected");
	}
}