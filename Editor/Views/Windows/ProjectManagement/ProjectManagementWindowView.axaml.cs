using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Editor.Views;

public partial class ProjectManagementWindowView : UserControl
{
	public ProjectManagementWindowView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}