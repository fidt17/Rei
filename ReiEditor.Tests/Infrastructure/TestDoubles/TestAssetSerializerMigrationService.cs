using ReiEditor.Models.Services.Assets.Migrations;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Records migration inputs and requires explicit JSON and file migration responses.</summary>
internal sealed class TestAssetSerializerMigrationService : IAssetSerializerMigrationService
{
    public List<(Type Type, string Json)> JsonCalls { get; } = new();
    public List<string> FileCalls { get; } = new();
    public Func<Type, string, AssetSerializerMigrationResult>? OnJson { get; set; }
    public Func<string, Task<bool>>? OnFile { get; set; }
    public AssetSerializerMigrationResult MigrateAssetJson(Type assetType, string sourceJson)
    {
        JsonCalls.Add((assetType, sourceJson));
        return (OnJson ?? throw new NotSupportedException())(assetType, sourceJson);
    }
    public Task<bool> TryMigrateAssetFile(string assetPath)
    {
        FileCalls.Add(assetPath);
        return (OnFile ?? throw new NotSupportedException())(assetPath);
    }
}
