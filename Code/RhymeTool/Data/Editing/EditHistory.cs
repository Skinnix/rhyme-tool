using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Editing;

public class EditHistory<TEntry>
	where TEntry : struct, IEditHistoryEntry
{
}

public interface IEditHistoryEntry
{
	string Label { get; }

	DelayedMetalineEditResult Undo();
	DelayedMetalineEditResult Redo();
}
