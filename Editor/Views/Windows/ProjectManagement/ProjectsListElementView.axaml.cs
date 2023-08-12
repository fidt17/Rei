using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Editor.Views;

public partial class ProjectsListElementView : UserControl
{
	public ProjectsListElementView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}