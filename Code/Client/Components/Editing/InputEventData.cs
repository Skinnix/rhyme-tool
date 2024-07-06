using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.Components.Editing;

public record JsInputEventData(string InputType, string Data, JsSheetSelectionRange Selection);
public record JsSheetSelectionRange(JsSheetSelectionAnchor Start, JsSheetSelectionAnchor End);
public record JsSheetSelectionAnchor(Guid Metaline, int Line, SliceSelectionAnchor Slice);
