using System;
using Newtonsoft.Json.Linq;

namespace ReiEditor.Models.Services.Assets.Migrations;

public interface IAssetSerializerMigration
{
    Type AssetType { get; }
    int FromVersion { get; }
    int ToVersion { get; }

    void Migrate(JObject assetJson);
}
