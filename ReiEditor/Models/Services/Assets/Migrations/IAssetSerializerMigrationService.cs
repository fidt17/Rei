using System;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Migrations;

public interface IAssetSerializerMigrationService
{
    AssetSerializerMigrationResult MigrateAssetJson(Type assetType, string sourceJson);
    Task<bool> TryMigrateAssetFile(string assetPath);
}
