//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Konves.ChordPro;

//namespace Skinnix.RhymeTool.Client.IO.Data;

//public class SongFile
//{
//	public const char MARKER = '#';
//	public const char ESCAPE = '\\';
//	public const char VALUE = ':';
//	public const char START = '{';
//	public const char END = '}';

//	public const char LINE_DIRECTIVE = MARKER;
//	public const char INLINE_DIRECTIVE_START = START;
//	public const char INLINE_DIRECTIVE_END = END;

//	public IList<SongLine> Lines { get; } = [];
//}

//public abstract class SongFileElement
//{
//	public abstract override string ToString();
//}

//public abstract class SongLine : SongFileElement
//{

//}

//public class DirectiveLine : SongLine
//{
//	public LineDirective Directive { get; }

//	public DirectiveLine(LineDirective directive)
//	{
//		Directive = directive;
//	}

//	public override string ToString() => Directive.ToString();
//}

//public abstract class Directive : SongFileElement
//{
//	public abstract string Key { get; }
//}

//public abstract class LineDirective : Directive
//{
//	public override string ToString() => $"{SongFile.MARKER}{Key}";
//}

//public abstract class LineStartDirective : LineDirective
//{
//}

//public abstract class InlineDirective : Directive
//{

//}
