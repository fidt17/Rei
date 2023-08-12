using System;
using System.Collections.ObjectModel;
using Editor.Utils;

namespace Editor.ViewModels.Controls;

public class ContextMenuViewModel : BaseViewModel
{
	public class ContextMenuOption
	{
		public RelayCommand Command { get; }
		public string Text { get; }

		public ContextMenuOption(string text, Action? callback = null)
		{
			Text = text;

			Command = new RelayCommand();
			Command.ExecutedEvent += callback;
		}
	}

	public ObservableCollection<ContextMenuOption> Options { get; } = new();

	public void AddOption(ContextMenuOption option) => Options.Add(option);
}