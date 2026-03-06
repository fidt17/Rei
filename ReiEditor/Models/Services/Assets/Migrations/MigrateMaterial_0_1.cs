using System;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.Services.Assets.Migrations;

public class MigrateMaterial_0_1 : IAssetSerializerMigration
{
    public Type AssetType => typeof(Material);
    public int FromVersion => 0;
    public int ToVersion => 1;

    public void Migrate(JObject assetJson)
    {
        // Reserved for legacy material payload normalization in the initial migration boundary.
    }
}
