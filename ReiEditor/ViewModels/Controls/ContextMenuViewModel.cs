using System;
using System.Collections.ObjectModel;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Controls;

public class ContextMenuViewModel : BaseViewModel
{
	public event Action? AnyCommandExecutedEvent;
	
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

	public void AddOption(ContextMenuOption option)
	{
		Options.Add(option);
		option.Command.ExecutedEvent += () => AnyCommandExecutedEvent?.Invoke();
	}
}