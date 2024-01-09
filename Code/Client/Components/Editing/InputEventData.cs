namespace Skinnix.RhymeTool.Client.Components.Editing;

public record InputEventData(string InputType, string Data, SheetSelectionRange Selection);
public record SheetSelectionRange(SheetSelectionAnchor Start, SheetSelectionAnchor End);
public record SheetSelectionAnchor(Guid Metaline, int Line, int Offset);
