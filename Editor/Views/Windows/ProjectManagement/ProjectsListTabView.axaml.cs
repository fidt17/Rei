using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Editor.Views.ProjectManagement;

public partial class ProjectsListTabView : UserControl
{
	public ProjectsListTabView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}