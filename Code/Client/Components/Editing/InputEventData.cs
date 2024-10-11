using System.Diagnostics.CodeAnalysis;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.Components.Editing;

public record InputEventData(string InputType, string? Data, LineSelection Selection, LineSelection? EditRange);

public record LineSelection(LineSelectionAnchor Start, LineSelectionAnchor End)
{
	[return: NotNullIfNotNull(nameof(range))]
	public static LineSelection? FromRange(MetalineSelectionRange? range)
		=> range is null ? null : new LineSelection(
			  new LineSelectionAnchor(range.StartMetaline.Guid, range.StartLineId, range.Range.Start),
			  new LineSelectionAnchor(range.EndMetaline.Guid, range.EndLineId, range.Range.End));
}

public record LineSelectionAnchor(Guid Metaline, int Line, int Offset);
