using System;
using Newtonsoft.Json.Linq;

namespace ReiEditor.Models.Services.Assets.Migrations;

public sealed class MigrateDataAsset_0_1 : IAssetSerializerMigration
{
    public Type AssetType => typeof(DataAsset);
    public int FromVersion => 0;
    public int ToVersion => 1;

    public void Migrate(JObject assetJson)
    {
        // Establishes versioned DataAsset serialization without changing legacy payload shape.
    }
}
