using System;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;

namespace ReiEditor.ViewModels.Common;

public class NavigationStore : BaseViewModel
{
	public event Action? ChangedEvent;

	#region ViewModel

	private BaseViewModel? _viewModel;
	public BaseViewModel ViewModel
	{
		get => _viewModel ?? new EmptyViewModel();
		private set
		{
			_viewModel?.Dispose();
			SetField(ref _viewModel, value);
			ChangedEvent?.Invoke();
		}
	}

	#endregion
	
    public T Navigate<T>(IFactory<T> factory) where T : BaseViewModel
    {
        var viewModel = factory.CreateInstance();
        ViewModel = viewModel;
        return viewModel;
    }
    
	public void LogOnNavigate<T>(ILogger<T> logger)
	{
		ChangedEvent += () =>
		{
			if (ViewModel != null)
			{
				logger.Log($"Navigate to {ViewModel.GetType().Name.Replace("ViewModel", "")}");
			}
		};
	}
}