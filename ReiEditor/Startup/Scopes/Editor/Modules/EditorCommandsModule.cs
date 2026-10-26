using Autofac;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public sealed class EditorCommandsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<EditorCommandService>().As<IEditorCommandService>();
        builder.RegisterType<EntityRenameCommand>().As<IEntityRenameCommand>();
        builder.RegisterDecorator<EntityRenameCommandDecorator, IEntityRenameCommand>();
        builder.RegisterType<SelectedEntityDeleteCommand>().As<ISelectedEntityDeleteCommand>();
        builder.RegisterType<SelectedEntityDuplicateCommand>().As<ISelectedEntityDuplicateCommand>();
        builder.RegisterType<SelectedEntityRenameCommand>().As<ISelectedEntityRenameCommand>();
        builder.RegisterType<CreateSceneEntityCommand>();
        builder.RegisterType<SaveProjectCommand>();

        builder.RegisterSingleton<ProjectAssetDeleteCommand>().As<IProjectAssetDeleteCommand>();
        builder.RegisterSingleton<ProjectAssetDuplicateCommand>().As<IProjectAssetDuplicateCommand>();
        builder.RegisterSingleton<ProjectAssetMoveCommand>().As<IProjectAssetMoveCommand>();
        builder.RegisterSingleton<ProjectAssetRenameCommand>().As<IProjectAssetRenameCommand>();
    }
}
