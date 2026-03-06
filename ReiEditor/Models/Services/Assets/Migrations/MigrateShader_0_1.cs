using System;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.Services.Assets.Migrations;

public class MigrateShader_0_1 : IAssetSerializerMigration
{
    public Type AssetType => typeof(Shader);
    public int FromVersion => 0;
    public int ToVersion => 1;

    public void Migrate(JObject assetJson)
    {
        // Reserved for legacy shader payload normalization in the initial migration boundary.
    }
}
