using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.IO;

namespace Skinnix.RhymeTool.Client;

public static partial class Program1
{
	private const string SONG_DATA = """
${CPSong[1]}\
$title: Everybody Hurts
$subtitle: REM
$define G: base-fret 1 frets 3 2 0 0 3 3
$define D4: base-fret 0 frets - - 0 0 3 -
$define E: base-fret 0 frets - 3 3 2 0 0
{start_of_tab}
Intro: E----------2-----------2-------------3-----------3-------
       B--------3---3-------3---3---------3---3-------3---3-----
       G------2-------2---2-------2-----0-------0---0-----------
       D----0-----------0---------------------------------------
       A--------------------------------------------------------
       E------------------------------3-----------3------------- (repeat)
{end_of_tab}

[D]When your day is [G]long and the [D]night, the night is [G]yours a[D]lone
[D]When you're sure you've had e[G]nough of this [D]life, well [G]hang on
{start_of_tab}
    E(low)-3-2-0-
{end_of_tab}
[E]Don't let yourself [A]go, [E]cause everybody [A]cries [E]and everybody[A] hurts some[D]times [G]
Sometimes everything is [D]wrong,   [G]now it's time to sing a[D]long
When your day is night alone [G]          (hold [D]on, hold on)
If you feel like letting go [G]           (hold [D]on)
If you think you've had too [G]much of this [D]life, well hang [G]on

{start_of_tab}
    E(low)-3-2-0-
{end_of_tab}
[E]Cause everybody [A]hurts, [E]take comfort in your [A]friends
[E]Everybody [A]hurts, [E]don't throw your [A]hands, oh [E]now, don't throw your [A]hands
[C]If you feel like you're [D4]alone, no, no, no, you're not [A]alone
{start_of_tab}
            D4 ->   E-0-----0-----0-----0--
                    B---3-----3-----3------
                    G-----0-----0-----0----
{end_of_tab}
	[D]If you're on your [G]own in this [D]life, the days and nights are [G]long
[D]When you think you've had too [G]much, with this [D]life, to hang [G]on

{start_of_tab}
    E(low)-3-2-0-
{end_of_tab}
[E]Well everybody [A]hurts, some[E]times 
Everybody [A]cries, [E]and everybody [A]hurts,[N.C.] ... some[D]times [G]
But everybody [D]hurts [G]sometimes so hold [D]on, hold [G]on, hold [D]on
Hold on, [G]hold on, [D]hold on, [G]hold on, [D]hold on
[G]Everybody [D]hurts [G]     [D]     [G]
[D]You are not alone [G]     [D]     [G]     [D]     [G]
""";

	public static void Main()
	{
		var lines = SONG_DATA.Split('\n');
		var songFile = SongFileImpl.TryCreate(lines[0].TrimEnd('\r'));
		if (songFile is null)
		{
			Console.WriteLine("Invalid song file header.");
			return;
		}

		var songLines = songFile.Parse(lines.Skip(1).Select(l => l.TrimEnd('\r'))).ToArray();
		var songString = songFile.Stringify(songLines);
	}

	private partial class SongFileImpl : SongFile
	{
		[GeneratedRegex(@"^(?<Sep>[^A-Za-z0-9\s])(?<DirS>[^A-Za-z0-9\s])CPSong(?<AttS>[^A-Za-z0-9\s])(?<Ver>\d+)(?<AttE>[^A-Za-z0-9\s])(?<DirE>[^A-Za-z0-9\s])(?<Esc>[^A-Za-z0-9\s])$")]
		private static partial Regex GenerateHeaderRegex();
		private static readonly Regex headerRegex = GenerateHeaderRegex();

		private SongFileImpl(Configuration configuration)
			: base(configuration)
		{ }

		public static SongFileImpl? TryCreate(string header)
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

			return new SongFileImpl(configuration);
		}

		public string CreateHeader(int version)
		{
			return $"{TokenConfiguration.SeparatorChar}{TokenConfiguration.DirectiveStartChar}CPSong{TokenConfiguration.AttachmentStartChar}{version}{TokenConfiguration.AttachmentEndChar}{TokenConfiguration.DirectiveEndChar}{TokenConfiguration.EscapeChar}";
		}

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
}
