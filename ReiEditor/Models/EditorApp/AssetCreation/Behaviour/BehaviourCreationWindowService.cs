using System;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;
using ReiEditor.Views.Windows.Editor.Project.AssetCreation;

namespace ReiEditor.Models.EditorApp.AssetCreation.Behaviour;

public class BehaviourCreationWindowService : IBehaviourCreationWindowService
{
    private CreateBehaviourAssetWindowView? _window;

    private readonly IFactory<CreateBehaviourAssetWindowViewModel> _viewModelFactory;
    private readonly IMainWindowService _mainWindowService;
    private readonly ILogger<BehaviourCreationWindowService> _logger;

    public BehaviourCreationWindowService(
        IFactory<CreateBehaviourAssetWindowViewModel> viewModelFactory,
        IMainWindowService mainWindowService,
        ILogger<BehaviourCreationWindowService> logger)
    {
        _viewModelFactory = viewModelFactory;
        _mainWindowService = mainWindowService;
        _logger = logger;
    }

    public void OpenBehaviourCreationWindow(string targetDirectory, Action onCreated)
    {
        if (_window != null)
        {
            _logger.LogWarning("Behaviour creation window is already opened.");
            return;
        }

        var vm = _viewModelFactory.CreateInstance(targetDirectory, onCreated);
        _window = new CreateBehaviourAssetWindowView
        {
            DataContext = vm
        };
        _mainWindowService.ShowDialog(_window);

        _window.Closed += (_, _) =>
        {
            vm.Dispose();
            _window = null;
        };
    }

    public void CloseBehaviourCreationWindow()
    {
        if (_window == null)
        {
            _logger.LogWarning("Cannot close behaviour creation window because it is not opened.");
            return;
        }

        _window.Close();
    }
}
