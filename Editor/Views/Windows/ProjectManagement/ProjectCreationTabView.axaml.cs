using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Editor.Views;

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