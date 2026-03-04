using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.BuildProject;
using ReiEditor.Views.Windows.Editor.BuildProject;

namespace ReiEditor.Models.EditorApp.ProjectBuildWindow;

public class ProjectBuildWindowService : IProjectBuildWindowService
{
    public IObservable<bool> IsOpened => _isOpened;

    private readonly Observable<bool> _isOpened = new(false);
    private readonly IFactory<BuildProjectWindowViewModel> _viewModelFactory;
    private readonly IMainWindowService _mainWindowService;
    private readonly ILogger<ProjectBuildWindowService> _logger;

    private BuildProjectWindowView? _window;

    public ProjectBuildWindowService(
        IFactory<BuildProjectWindowViewModel> viewModelFactory,
        IMainWindowService mainWindowService,
        ILogger<ProjectBuildWindowService> logger)
    {
        _viewModelFactory = viewModelFactory;
        _mainWindowService = mainWindowService;
        _logger = logger;
    }

    public void OpenWindow()
    {
        if (_isOpened.Value)
        {
            _logger.LogError("Cannot open build project window because it is already opened.");
            return;
        }

        var viewModel = _viewModelFactory.CreateInstance();
        _window = new BuildProjectWindowView
        {
            DataContext = viewModel,
        };
        _mainWindowService.ShowDialog(_window);
        _isOpened.Value = true;

        _window.Closed += (_, _) =>
        {
            _isOpened.Value = false;
            viewModel.Dispose();
            _window = null;
        };
    }

    public void CloseWindow()
    {
        if (_window == null)
        {
            _logger.LogError("Cannot close build project window because it is not opened.");
            return;
        }

        _window.Close();
    }
}
