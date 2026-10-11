using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public sealed class HierarchyNodeTemplate : IDataTemplate
{
    // Root and child presenters share views when a node changes parent. Removed VMs remain collectible.
    private static readonly ConditionalWeakTable<HierarchyNodeViewModel, HierarchyNode> _views = new();

    public bool Match(object? data) => data is HierarchyNodeViewModel;

    public Control? Build(object? data) => data is HierarchyNodeViewModel node
        ? _views.GetValue(node, key => new HierarchyNode { DataContext = key })
        : null;
}
