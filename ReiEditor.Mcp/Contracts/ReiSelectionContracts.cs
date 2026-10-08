using System.Text.Json.Serialization;

namespace ReiEditor.Mcp.Contracts;

public sealed record ReiSelectionState(string Source, string Status, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] IReadOnlyList<int>? SelectedIds);
