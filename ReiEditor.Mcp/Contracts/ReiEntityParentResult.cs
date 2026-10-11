namespace ReiEditor.Mcp.Contracts;

public sealed record ReiEntityParentResult(bool Accepted, int EntityId, int ParentId, int Order);
