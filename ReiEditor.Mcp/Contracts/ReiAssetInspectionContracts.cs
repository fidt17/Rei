using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReiEditor.Mcp.Contracts;

public sealed record ReiAssetState(string AssetId, string Source, string Status, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? Values);
public sealed record ReiAssetSelection(string AssetId, string AssetName, bool MonitorSupported);
