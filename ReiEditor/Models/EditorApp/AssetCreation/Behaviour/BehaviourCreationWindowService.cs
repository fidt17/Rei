using System;
using ReiEditor.Models.EditorApp.AssetCreation.Common;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;
using ReiEditor.Views.Windows.Editor.Project.AssetCreation;

namespace ReiEditor.Models.EditorApp.AssetCreation.Behaviour;

public class BehaviourCreationWindowService : IBehaviourCreationWindowService
{
    private readonly IFactory<CreateBehaviourAssetWindowViewModel> _viewModelFactory;
    private readonly SingleDialogWindowCoordinator _windowCoordinator;
    private readonly ILogger<BehaviourCreationWindowService> _logger;

    public BehaviourCreationWindowService(
        IFactory<CreateBehaviourAssetWindowViewModel> viewModelFactory,
        IMainWindowService mainWindowService,
        ILogger<BehaviourCreationWindowService> logger)
    {
        _viewModelFactory = viewModelFactory;
        _logger = logger;
        _windowCoordinator = new SingleDialogWindowCoordinator(mainWindowService, _logger);
    }

    public void OpenBehaviourCreationWindow(string targetDirectory, Action onCreated)
    {
        _windowCoordinator.Open(
            () => _viewModelFactory.CreateInstance(targetDirectory, onCreated),
            vm => new CreateBehaviourAssetWindowView { DataContext = vm });
    }

    public void CloseBehaviourCreationWindow()
    {
        _windowCoordinator.Close();
    }
}
