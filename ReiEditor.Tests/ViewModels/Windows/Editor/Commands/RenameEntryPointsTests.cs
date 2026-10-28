using Autofac;
using Avalonia;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;
using ReiEditor.Views.Controls.TextBoxes;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Commands;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "EditorCommands")]
public sealed class RenameEntryPointsTests
{
    private sealed class Actions : ISelectedEntityActionService
    {
        public event Action<int>? RenameEntityRequested { add { } remove { } }
        public bool DeleteSelectedEntity() => throw new NotSupportedException();
        public bool DuplicateSelectedEntity() => throw new NotSupportedException();
        public bool RequestRenameSelectedEntity() => throw new NotSupportedException();
    }

    [AvaloniaFact]
    public async Task HierarchyAndInspectorRecordCompletedRenamesInOneHistory()
    {
        var entity = new GameEntity(1, "Before");
        var scenes = new TestSceneManagementService(entity);
        var management = new TestEntityManagementService { OnRename = (target, name) => target.SetName(name) };
        var builder = new ContainerBuilder();
        builder.RegisterModule<EditorCommandsModule>();
        builder.RegisterInstance(management).As<IEntityManagementService>();
        builder.RegisterInstance(scenes).As<ISceneManagementService>();
        builder.RegisterInstance(new TestEngineRunner()).As<IEngineRunner>();
        builder.RegisterGeneric(typeof(TestLogger<>)).As(typeof(ILogger<>));
        var api = new EngineApi(new TestLogger<EngineApi>());
        builder.RegisterInstance(api).As<IEngineApi>();
        builder.RegisterInstance(new TestEngineNativeAccess(api)).As<IEngineNativeAccess>();
        using var container = builder.Build();
        var rename = container.Resolve<IEntityRenameCommand>();
        var commands = container.Resolve<IEditorCommandService>();
        var selection = new SelectionService(new TestEntityApi());
        using var hierarchy = new HierarchyNodeViewModel(scenes.Scene.Value!.Hierarchy.GetNode(entity)!, rename, new Actions(), selection);
        using var inspector = new EntityInfoComponentDrawerViewModel(entity, rename, new TestLogger<EntityInfoComponentDrawerViewModel>());
        hierarchy.ConfirmRenameCommand.Execute("Hierarchy");
        Assert.Equal("Hierarchy", inspector.EntityName);
        var input = new ReiTextBox { DataContext = inspector };
        using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(nameof(inspector.EntityName)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Hierarchy", input.Text);
        input.TextInternal = "I";
        input.TextInternal = "Inspector";
        Assert.Equal("Hierarchy", entity.Name);
        input.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Inspector", entity.Name);
        await commands.UndoAsync();
        Assert.Equal("Hierarchy", entity.Name);
        Assert.Equal("Hierarchy", inspector.EntityName);
        await commands.UndoAsync();
        Assert.Equal("Before", entity.Name);
        Assert.False(commands.CanUndo);
        await commands.RedoAsync();
        await commands.RedoAsync();
        Assert.Equal("Inspector", entity.Name);
        Assert.Equal("Inspector", hierarchy.Name.Value);
        Assert.False(commands.CanRedo);
    }
}
