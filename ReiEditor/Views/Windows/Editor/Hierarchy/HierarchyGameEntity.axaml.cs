using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Windows.Editor.Hierarchy;

public partial class HierarchyGameEntity : UserControl
{
	public HierarchyGameEntity()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}