using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Tests.Infrastructure.Fixtures;

internal sealed class HierarchyDragFixture : IDisposable
{
    private sealed class Factory<T>(Func<object[], T> create) : IFactory<T> where T : class
    {
        public T CreateInstance() => create([]);
        public T CreateInstance(params object[] parameters) => create(parameters);
    }

    private sealed class Rename : IEntityRenameCommand
    {
        public Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget target) => throw new NotSupportedException();
    }

    private sealed class Actions : ISelectedEntityActionService
    {
        public event Action<int>? RenameEntityRequested { add { } remove { } }
        public bool DeleteSelectedEntity() => throw new NotSupportedException();
        public bool DuplicateSelectedEntity() => throw new NotSupportedException();
        public bool RequestRenameSelectedEntity() => throw new NotSupportedException();
    }

    public HierarchyWindowViewModel ViewModel { get; }

    public HierarchyDragFixture(Hierarchy<GameEntity> hierarchy, IEntityManagementService management,
        ISceneManagementService? scenes = null, IEditorCommandService? commands = null, IEngineRunner? engine = null)
    {
        var selection = new SelectionService(new TestEntityApi());
        ViewModel = new(hierarchy, selection,
            new Factory<HierarchyNodeViewModel>(args => new((HierarchyNode<GameEntity>)args[0], new Rename(), new Actions(), selection)),
            new Factory<CreateSceneEntityCommand>(_ => new(scenes ?? new TestSceneManagementService(), management, commands ?? new EditorCommandService(), engine ?? new TestEngineRunner())),
            new Actions(), management);
    }

    public void Dispose() => ViewModel.Dispose();
}
