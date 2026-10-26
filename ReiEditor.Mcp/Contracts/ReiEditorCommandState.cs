namespace ReiEditor.Mcp.Contracts;

public sealed record ReiEditorCommandState(bool IsBusy, bool CanUndo, bool CanRedo);
