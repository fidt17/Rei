using System;
using ReiEditor.Models.EditorApp.AssetCreation.Common;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;
using ReiEditor.Views.Windows.Editor.Project.AssetCreation;

namespace ReiEditor.Models.EditorApp.AssetCreation.Material;

public class MaterialCreationWindowService : IMaterialCreationWindowService
{
    private readonly IFactory<CreateMaterialAssetWindowViewModel> _viewModelFactory;
    private readonly SingleDialogWindowCoordinator _windowCoordinator;
    private readonly ILogger<MaterialCreationWindowService> _logger;

    public MaterialCreationWindowService(
        IFactory<CreateMaterialAssetWindowViewModel> viewModelFactory,
        IMainWindowService mainWindowService,
        ILogger<MaterialCreationWindowService> logger)
    {
        _viewModelFactory = viewModelFactory;
        _logger = logger;
        _windowCoordinator = new SingleDialogWindowCoordinator(mainWindowService, _logger);
    }

    public void OpenMaterialCreationWindow(string targetDirectory, Action onCreated)
    {
        _windowCoordinator.Open(
            () => _viewModelFactory.CreateInstance(targetDirectory, onCreated),
            vm => new CreateMaterialAssetWindowView { DataContext = vm });
    }

    public void CloseMaterialCreationWindow()
    {
        _windowCoordinator.Close();
    }
}
