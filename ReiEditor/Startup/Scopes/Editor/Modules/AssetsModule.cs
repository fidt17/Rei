using Autofac;
using ReiEditor.Models.EditorApp.AssetCreation.Behaviour;
using ReiEditor.Models.EditorApp.AssetCreation.Material;
using ReiEditor.Models.EditorApp.AssetCreation.Shader;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation;
using ReiEditor.Models.Services.Assets.Creation.Behaviour;
using ReiEditor.Models.Services.Assets.Creation.Material;
using ReiEditor.Models.Services.Assets.Creation.Shader;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Migrations;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class AssetsModule : Module
{
    protected override void Load(ContainerBuilder b)
    {
        b.RegisterSingleton<AssetRegistry>().As<IAssetRegistry>();
        b.RegisterSingleton<AssetsService>().As<IAssetsService>();
        b.RegisterSingleton<AssetTypeMapper>().As<IAssetTypeMapper>();
        b.RegisterSingleton<MetaFilesService>().As<IMetaFilesService>();
        b.RegisterSingleton<AssetImporter>().As<IAssetImporter>();
        b.RegisterSingleton<AssetOperationsService>().As<IAssetOperationsService>();
        b.RegisterSingleton<AssetSearchService>().As<IAssetSearchService>();
        b.RegisterSingleton<AssetRuntimeSyncService>().As<IAssetRuntimeSyncService>();
        b.RegisterSingleton<ShaderUniformParser>().As<IShaderUniformParser>();
        b.RegisterSingleton<ShaderRegistry>().As<IShaderRegistry>();

        b.RegisterSingleton<AssetCreator>().As<IAssetCreator>();
        b.RegisterSingleton<BehaviourCreationUtility>().As<IBehaviourCreationUtility>();
        b.RegisterSingleton<MaterialCreationUtility>().As<IMaterialCreationUtility>();
        b.RegisterSingleton<ShaderCreationUtility>().As<IShaderCreationUtility>();
        b.RegisterSingleton<BehaviourCreationWindowService>().As<IBehaviourCreationWindowService>();
        b.RegisterSingleton<MaterialCreationWindowService>().As<IMaterialCreationWindowService>();
        b.RegisterSingleton<ShaderCreationWindowService>().As<IShaderCreationWindowService>();

        b.RegisterSingleton<EngineResourcesImporter>().As<IEngineResourcesImporter>();
        b.RegisterSingleton<SerializableObjectsRegistry>().As<ISerializableObjectsRegistry>();

        b.RegisterSingleton<BehaviourRegistry>().As<IBehaviourRegistry>();
        b.RegisterSingleton<BehaviourFileUtility>().As<IBehaviourFileUtility>();
        b.RegisterSingleton<SourceFilesUtility>();
        b.RegisterSingleton<BehaviourComponentsService>().As<IBehaviourComponentsService>();

        b.RegisterSingleton<ProjectAssetFocusService>().As<IProjectAssetFocusService>();
        
        RegisterMigrations(b);
    }

    private static void RegisterMigrations(ContainerBuilder b)
    {
        b.RegisterSingleton<AssetSerializerMigrationService>().As<IAssetSerializerMigrationService>();
        b.RegisterSingleton<MigrateScene_0_1>().As<IAssetSerializerMigration>();
        b.RegisterSingleton<MigrateMaterial_0_1>().As<IAssetSerializerMigration>();
        b.RegisterSingleton<MigrateShader_0_1>().As<IAssetSerializerMigration>();
        b.RegisterSingleton<MigrateBuildScenesConfiguration_0_1>().As<IAssetSerializerMigration>();
    }
}
