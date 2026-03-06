using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets.Migrations;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets;

public abstract class Asset : IOnSerialization
{
    [JsonIgnore]
    public string AssetId { get; private set; } = "";
    
    [JsonIgnore]
    public string FullPath { get; private set; } = "";

    [JsonProperty("SerializerVersion")]
    public int SerializerVersion { get; private set; } = AssetSerializerVersions.LEGACY_VERSION;

    public void SetAssetInfo(AssetInfo assetInfo)
    {
        AssetId = assetInfo.Meta.AssetId;
        FullPath = assetInfo.FullPath;
    }

    public void SetSerializerVersion(int serializerVersion)
    {
        SerializerVersion = serializerVersion < AssetSerializerVersions.LEGACY_VERSION
            ? AssetSerializerVersions.LEGACY_VERSION
            : serializerVersion;
    }

    public void OnSerialization()
    {
        SetSerializerVersion(AssetSerializerVersions.GetCurrentVersion(GetType()));
    }
}
