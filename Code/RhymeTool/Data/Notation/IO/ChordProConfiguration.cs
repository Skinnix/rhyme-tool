using Konves.ChordPro;
using Konves.ChordPro.DirectiveHandlers;
using Konves.ChordPro.Directives;

namespace Skinnix.RhymeTool.Data.Notation.IO;

public class ChordProConfiguration
{
	public static ChordProConfiguration Default { get; } = new();

	public IReadOnlyCollection<DirectiveHandler> DirectiveHandlers { get; }

	public ChordProConfiguration()
	{
		DirectiveHandlers = [
			new StartOfSectionDirective.Handler("verse", "sov"), new EndOfSectionDirective.Handler("verse", "eov"),
			new StartOfSectionDirective.Handler("chorus", "soc"), new EndOfSectionDirective.Handler("chorus", "eoc"),
			new StartOfSectionDirective.Handler("bridge", "sob"), new EndOfSectionDirective.Handler("bridge", "eob"),
		];
	}

	public StartOfSectionDirective StartVerse(string? label = null)
		=> new StartOfSectionDirective("verse", label);

	public StartOfSectionDirective StartChorus(string? label = null)
		=> new StartOfSectionDirective("chorus", label);

	public StartOfSectionDirective StartBridge(string? label = null)
		=> new StartOfSectionDirective("bridge", label);

	public sealed class StartOfSectionDirective : Directive
	{
		public string SectionKey { get; set; }
		public string? Label { get; set; }

		public StartOfSectionDirective(string sectionKey, string? label = null)
		{
			SectionKey = sectionKey;
			Label = label;
		}

		public EndOfSectionDirective CreateEnd()
			=> new EndOfSectionDirective(SectionKey);

		public sealed class Handler(string sectionKey, string? shortName) : DirectiveHandler<StartOfSectionDirective>
		{
			public override ComponentPresence SubKey => ComponentPresence.NotAllowed;
			public override ComponentPresence Value => ComponentPresence.Optional;

			public override string LongName { get; } = "start_of_" + sectionKey;
			public override string? ShortName { get; } = shortName;

			protected override bool TryCreate(DirectiveComponents components, out Directive directive)
			{
				var value = components.Value;
				if (value == string.Empty)
					value = null;

				directive = new StartOfSectionDirective(components.Key, value);
				return true;
			}
		}
	}

	public sealed class EndOfSectionDirective : Directive
	{
		public string SectionKey { get; set; }

		public EndOfSectionDirective(string sectionKey)
		{
			SectionKey = sectionKey;
		}

		public sealed class Handler(string sectionKey, string? shortName) : DirectiveHandler<StartOfSectionDirective>
		{
			public override ComponentPresence SubKey => ComponentPresence.NotAllowed;
			public override ComponentPresence Value => ComponentPresence.Optional;

			public override string LongName { get; } = "end_of_" + sectionKey;
			public override string? ShortName { get; } = shortName;

			protected override bool TryCreate(DirectiveComponents components, out Directive directive)
			{
				var value = components.Value;
				if (value == string.Empty)
					value = null;

				directive = new StartOfSectionDirective(components.Key, value);
				return true;
			}
		}
	}
}
