using System;
using System.Windows.Input;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class MoveNodeCommand : ICommand
{
    public class MoveArgs
    {
        public Hierarchy.Node? Parent { get; }
        public int ChildIdx { get; }

        public MoveArgs(Hierarchy.Node? parent, int childIdx)
        {
            Parent = parent;
            ChildIdx = childIdx;
        }
    }

    private readonly IEntityManagementService _entityManagementService;
    private readonly Hierarchy.Node _node;

    public MoveNodeCommand(Hierarchy.Node node, IEntityManagementService entityManagementService)
    {
        _node = node;
        _entityManagementService = entityManagementService;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (parameter is not MoveArgs args) return;

        _entityManagementService.SetParent(_node.Entity, args.Parent?.Entity, args.ChildIdx);
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CanExecuteChanged;
}