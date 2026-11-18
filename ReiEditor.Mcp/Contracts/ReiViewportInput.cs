namespace ReiEditor.Mcp.Contracts;

public sealed record ReiViewportInputEvent(string Type, double X = 0, double Y = 0, int Code = 0);
public sealed record ReiViewportInputResult(string Status, int EventCount);
