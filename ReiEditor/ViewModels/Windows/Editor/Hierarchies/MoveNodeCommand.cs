using System;
using System.Windows.Input;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class MoveNodeCommand : ICommand
{
    public class MoveArgs
    {
        public HierarchyNode<GameEntity>? Parent { get; }
        public int ChildIdx { get; }

        public MoveArgs(HierarchyNode<GameEntity>? parent, int childIdx)
        {
            Parent = parent;
            ChildIdx = childIdx;
        }
    }

    private readonly IEntityManagementService _entityManagementService;
    private readonly HierarchyNode<GameEntity> _node;

    public MoveNodeCommand(HierarchyNode<GameEntity> node, IEntityManagementService entityManagementService)
    {
        _node = node;
        _entityManagementService = entityManagementService;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (parameter is not MoveArgs args) return;

        _entityManagementService.SetParent(_node.Content, args.Parent?.Content, args.ChildIdx);
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CanExecuteChanged;
}