using Avalonia.Controls;
using Avalonia.Input;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public partial class HierarchyNode : UserControl
{
	public HierarchyNode()
	{
		InitializeComponent();
	}

	private void RootBorder_OnKeyDown(object? sender, KeyEventArgs e)
	{
		System.Console.WriteLine(e.Key);
		if (e.Key != Key.Delete) return;
		
		if (DataContext is HierarchyNodeViewModel vm && vm.Selected.Value)
		{
			vm.DeleteCommand.Execute(null);
		}
	}
}