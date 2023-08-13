using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Windows.ProjectManagement;

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