using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets;

[Trait("Category", "FileSystem")]
[Trait("Area", "HDR")]
public sealed class DefaultRendererSettingsServiceTests
{
    private sealed class Context : IDisposable
    {
        public TemporaryProjectFixture Project { get; } = new();
        public AssetRegistry Registry { get; } = new(new TestLogger<AssetRegistry>());
        public AssetsService Assets { get; }
        public DefaultRendererSettingsService Service { get; }
        public DataAssetTypeRegistry Types { get; }
        public string Path => Project.Resources.GetProjectPath("Settings/Rendering/Renderer Settings.asset");

        public Context()
        {
            var serializer = new JsonSerializer();
            var metas = new MetaFilesService(Project.Resources, serializer, new TestLogger<MetaFilesService>());
            var creator = new AssetCreator(Project.Resources, serializer, new TestLogger<AssetCreator>(), Registry, metas);
            Types = new DataAssetTypeRegistry(Project.Resources, metas, creator, new TestLogger<DataAssetTypeRegistry>(), null!);
            var objects = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
            objects.Replace([], [new SerializableEnum { EnumName = "ToneMappingMode", Options = new() { ["Off"] = 0, ["Reinhard"] = 1 } }]);
            var properties = new SerializedPropertiesService(objects, new TestLogger<SerializedPropertiesService>());
            var schema = new DataAssetSchemaService(Types, properties, new TestLogger<DataAssetSchemaService>());
            var active = new ActiveProjectService(new TestLogger<ActiveProjectService>());
            active.OpenProject(Project.Project);
            var migrations = new TestAssetSerializerMigrationService { OnJson = (_, json) => new(json, 0, 0, false) };
            Assets = new AssetsService(new TestLogger<AssetsService>(), Project.Resources, serializer, migrations, active, new EditorProceduresService(), Registry, [schema]);
            Service = new DefaultRendererSettingsService(Registry, Types, schema, Assets, creator, Project.Resources);
        }

        public Task Register() => Types.RefreshAsync([new SerializableObjectInfo("rei::render", "RendererSettings", false,
            new ObjectFile<string>("", Project.Resources.GetScriptsPath("RendererSettings.h")),
            new() { ["_exposure"] = Property(SerializedTypeEnum.Float, "f32", "0"), ["_toneMapping"] = Property(SerializedTypeEnum.Enum, "ToneMappingMode", "Reinhard") }, "RendererSettings.h")]);

        public void Dispose() => Project.Dispose();
    }

    [Fact]
    public async Task CurrentExposureSurvivesLoadAndSave()
    {
        using var context = new Context();
        await context.Register();
        await context.Service.EnsureCreated();
        var root = JObject.Parse(await File.ReadAllTextAsync(context.Path));
        var values = (JObject)root["SerializedData"]!;
        values["_exposure"]!["Value"] = -2.5;
        await File.WriteAllTextAsync(context.Path, root.ToString());
        context.Assets.Unload(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS);
        var asset = await context.Assets.Load<DataAsset>(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS);
        Assert.NotNull(asset);
        Assert.False(asset.HasProperty("_exposureEV"));
        Assert.Equal(-2.5, Convert.ToDouble(asset.GetProperty("_exposure").Value));
        await context.Assets.SaveProject();
        var saved = JObject.Parse(await File.ReadAllTextAsync(context.Path))["SerializedData"]!;
        Assert.Null(saved["_exposureEV"]);
        Assert.Equal(-2.5, saved["_exposure"]!["Value"]!.Value<double>());
    }

    [Fact]
    public async Task CreatesDefaultsOnceAndPreservesEditsAndRenamedLocation()
    {
        using var context = new Context();
        await context.Register();
        await context.Service.EnsureCreated();
        var initial = await context.Assets.Load<DataAsset>(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS);
        Assert.NotNull(initial);
        Assert.Equal(0.0, Convert.ToDouble(initial.GetProperty("_exposure").Value));
        Assert.Equal(1, initial.GetProperty("_toneMapping").Value);
        var original = await File.ReadAllTextAsync(context.Path);
        Assert.Equal(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS, JObject.Parse(await File.ReadAllTextAsync(context.Path + ".meta"))["AssetId"]!.Value<string>());
        initial.GetProperty("_exposure").Value = -2.5;
        await context.Service.EnsureCreated();
        Assert.Equal(-2.5, initial.GetProperty("_exposure").Value);
        Assert.Equal(original, await File.ReadAllTextAsync(context.Path));
        var renamed = context.Project.Resources.GetProjectPath("Settings/Rendering/Renamed.asset");
        File.Move(context.Path, renamed);
        File.Move(context.Path + ".meta", renamed + ".meta");
        context.Registry.UpdateRegistryPath(context.Path, renamed);
        context.Assets.Unload(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS);
        await context.Service.EnsureCreated();
        Assert.False(File.Exists(context.Path));
        Assert.Single(context.Registry.GetAllAssets());
    }

    [Fact]
    public async Task MissingFileIsRecreatedWithSameIdAfterRegistryRefresh()
    {
        using var context = new Context();
        await context.Register();
        await context.Service.EnsureCreated();
        File.Delete(context.Path);
        File.Delete(context.Path + ".meta");
        context.Assets.Unload(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS);
        context.Registry.UnregisterByPath(context.Path);
        await context.Service.EnsureCreated();
        Assert.True(File.Exists(context.Path));
        Assert.Single(context.Registry.GetAllAssets());
        Assert.NotNull(await context.Assets.Load<DataAsset>(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS));
    }

    [Fact]
    public async Task OccupiedNameAndOtherProfilesArePreserved()
    {
        using var context = new Context();
        await context.Register();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(context.Path)!);
        await File.WriteAllTextAsync(context.Path, "existing file");
        await context.Service.EnsureCreated();
        Assert.Equal("existing file", await File.ReadAllTextAsync(context.Path));
        var created = await context.Assets.Load<DataAsset>(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS);
        Assert.NotNull(created);
        Assert.NotEqual(context.Path, created.FullPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingTypeOrWrongAssetDoesNotCreateReplacement(bool wrongAsset)
    {
        using var context = new Context();
        if (wrongAsset)
        {
            await context.Register();
            context.Registry.RegisterNewAssets([new AssetInfo(new AssetMeta(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS), context.Path)]);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.EnsureCreated());
        Assert.False(File.Exists(context.Path));
    }

    private static SerializableObjectInfo.SerializedPropertyData Property(SerializedTypeEnum type, string source, string value) => new(type, source, null, SerializedTypeEnum.Invalid, null, null, value, false);
}
