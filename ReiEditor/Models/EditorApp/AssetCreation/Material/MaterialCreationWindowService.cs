using System;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;
using ReiEditor.Views.Windows.Editor.Project.AssetCreation;

namespace ReiEditor.Models.EditorApp.AssetCreation.Material;

public class MaterialCreationWindowService : IMaterialCreationWindowService
{
    private CreateMaterialAssetWindowView? _window;

    private readonly IFactory<CreateMaterialAssetWindowViewModel> _viewModelFactory;
    private readonly IMainWindowService _mainWindowService;
    private readonly ILogger<MaterialCreationWindowService> _logger;

    public MaterialCreationWindowService(
        IFactory<CreateMaterialAssetWindowViewModel> viewModelFactory,
        IMainWindowService mainWindowService,
        ILogger<MaterialCreationWindowService> logger)
    {
        _viewModelFactory = viewModelFactory;
        _mainWindowService = mainWindowService;
        _logger = logger;
    }

    public void OpenMaterialCreationWindow(string targetDirectory, Action onCreated)
    {
        if (_window != null)
        {
            _logger.LogWarning("Material creation window is already opened.");
            return;
        }

        var vm = _viewModelFactory.CreateInstance(targetDirectory, onCreated);
        _window = new CreateMaterialAssetWindowView
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

    public void CloseMaterialCreationWindow()
    {
        if (_window == null)
        {
            _logger.LogWarning("Cannot close material creation window because it is not opened.");
            return;
        }

        _window.Close();
    }
}
