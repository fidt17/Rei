using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Editor.Views;

public partial class ProjectManagementTabView : UserControl
{
	public ProjectManagementTabView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}