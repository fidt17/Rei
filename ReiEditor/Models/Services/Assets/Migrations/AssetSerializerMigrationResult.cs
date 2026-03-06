namespace ReiEditor.Models.Services.Assets.Migrations;

public readonly record struct AssetSerializerMigrationResult(
    string Json,
    int SourceVersion,
    int TargetVersion,
    bool IsUpdated);
