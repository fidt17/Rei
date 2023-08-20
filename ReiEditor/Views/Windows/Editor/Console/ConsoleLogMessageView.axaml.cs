using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Windows.Editor.Console;

public partial class ConsoleLogMessageView : UserControl
{
	public ConsoleLogMessageView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}