using System;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;
using ReiEditor.Views.Windows.Editor.Project.AssetCreation;

namespace ReiEditor.Models.EditorApp.AssetCreation.Shader;

public class ShaderCreationWindowService : IShaderCreationWindowService
{
    private CreateShaderAssetWindowView? _window;

    private readonly IFactory<CreateShaderAssetWindowViewModel> _viewModelFactory;
    private readonly IMainWindowService _mainWindowService;
    private readonly ILogger<ShaderCreationWindowService> _logger;

    public ShaderCreationWindowService(
        IFactory<CreateShaderAssetWindowViewModel> viewModelFactory,
        IMainWindowService mainWindowService,
        ILogger<ShaderCreationWindowService> logger)
    {
        _viewModelFactory = viewModelFactory;
        _mainWindowService = mainWindowService;
        _logger = logger;
    }

    public void OpenShaderCreationWindow(string targetDirectory, Action onCreated)
    {
        if (_window != null)
        {
            _logger.LogWarning("Shader creation window is already opened.");
            return;
        }

        var vm = _viewModelFactory.CreateInstance(targetDirectory, onCreated);
        _window = new CreateShaderAssetWindowView
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

    public void CloseShaderCreationWindow()
    {
        if (_window == null)
        {
            _logger.LogWarning("Cannot close shader creation window because it is not opened.");
            return;
        }

        _window.Close();
    }
}
