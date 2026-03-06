using System;
using ReiEditor.Models.EditorApp.AssetCreation.Common;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;
using ReiEditor.Views.Windows.Editor.Project.AssetCreation;

namespace ReiEditor.Models.EditorApp.AssetCreation.Shader;

public class ShaderCreationWindowService : IShaderCreationWindowService
{
    private readonly IFactory<CreateShaderAssetWindowViewModel> _viewModelFactory;
    private readonly SingleDialogWindowCoordinator _windowCoordinator;
    private readonly ILogger<ShaderCreationWindowService> _logger;

    public ShaderCreationWindowService(
        IFactory<CreateShaderAssetWindowViewModel> viewModelFactory,
        IMainWindowService mainWindowService,
        ILogger<ShaderCreationWindowService> logger)
    {
        _viewModelFactory = viewModelFactory;
        _logger = logger;
        _windowCoordinator = new SingleDialogWindowCoordinator(mainWindowService, _logger);
    }

    public void OpenShaderCreationWindow(string targetDirectory, Action onCreated)
    {
        _windowCoordinator.Open(
            () => _viewModelFactory.CreateInstance(targetDirectory, onCreated),
            vm => new CreateShaderAssetWindowView { DataContext = vm });
    }

    public void CloseShaderCreationWindow()
    {
        _windowCoordinator.Close();
    }
}
