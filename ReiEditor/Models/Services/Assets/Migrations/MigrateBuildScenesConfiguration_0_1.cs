using System;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Assets.Migrations;

public class MigrateBuildScenesConfiguration_0_1 : IAssetSerializerMigration
{
    public Type AssetType => typeof(BuildScenesConfiguration);
    public int FromVersion => 0;
    public int ToVersion => 1;

    public void Migrate(JObject assetJson)
    {
        // Reserved for legacy build scenes configuration payload normalization in the initial migration boundary.
    }
}
