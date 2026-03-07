using System;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Assets.Migrations;

public class MigrateScene_0_1 : IAssetSerializerMigration
{
    public Type AssetType => typeof(Scene);
    public int FromVersion => 0;
    public int ToVersion => 1;

    public void Migrate(JObject assetJson)
    {
        // Reserved for legacy scene payload normalization in the initial migration boundary.
    }
}
