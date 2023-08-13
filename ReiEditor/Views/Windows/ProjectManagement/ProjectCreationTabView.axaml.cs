using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Windows.ProjectManagement;

public partial class ProjectCreationTabView : UserControl
{
	public ProjectCreationTabView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}