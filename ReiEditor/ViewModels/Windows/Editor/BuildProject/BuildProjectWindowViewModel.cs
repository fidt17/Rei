using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReiEditor.Models.EditorApp.ProjectBuildWindow;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.ProjectBuild;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;
using ReactiveUI;

namespace ReiEditor.ViewModels.Windows.Editor.BuildProject;

public class BuildProjectWindowViewModel : BaseViewModel
{
    public RelayCommand BuildCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SelectOutputPathCommand { get; }
    public RelayCommand SelectIconPathCommand { get; }
    public RelayCommand SelectDebugConfigurationCommand { get; }
    public RelayCommand SelectReleaseConfigurationCommand { get; }

    private readonly IProjectBuildService _projectBuildService;
    private readonly IProjectBuildWindowService _windowService;
    private readonly ProjectBuildOutputPathUtility _outputPathUtility;
    private readonly IStorageProvider _storageProvider;

    private CancellationTokenSource? _buildCancellationTokenSource;
    private DispatcherTimer? _elapsedTimer;
    private DateTime _buildStartedAtUtc;

    private string _outputPath = string.Empty;
    public string OutputPath
    {
        get => _outputPath;
        set => SetField(ref _outputPath, value);
    }

    private string _iconPath = string.Empty;
    public string IconPath
    {
        get => _iconPath;
        set => SetField(ref _iconPath, value);
    }

    private bool _showConsole = true;
    public bool ShowConsole
    {
        get => _showConsole;
        set => SetField(ref _showConsole, value);
    }

    private BuildConfigurationEnum _selectedConfiguration = BuildConfigurationEnum.Release;
    public BuildConfigurationEnum SelectedConfiguration
    {
        get => _selectedConfiguration;
        set
        {
            if (!SetField(ref _selectedConfiguration, value)) return;
            OutputPath = _outputPathUtility.GetDefaultPackageOutputPath(value);
            this.RaisePropertyChanged(nameof(IsDebugSelected));
            this.RaisePropertyChanged(nameof(IsReleaseSelected));
        }
    }

    private bool _isBuildInProgress;
    public bool IsBuildInProgress
    {
        get => _isBuildInProgress;
        private set
        {
            if (!SetField(ref _isBuildInProgress, value)) return;
            BuildCommand.InvokeCanExecuteChanged();
        }
    }

    private bool _isCancelRequested;
    public bool IsCancelRequested
    {
        get => _isCancelRequested;
        private set => SetField(ref _isCancelRequested, value);
    }

    private string _progressStatus = string.Empty;
    public string ProgressStatus
    {
        get => _progressStatus;
        private set => SetField(ref _progressStatus, value);
    }

    private double _progressValue;
    public double ProgressValue
    {
        get => _progressValue;
        private set => SetField(ref _progressValue, value);
    }

    private string _elapsedTimeText = "00:00";
    public string ElapsedTimeText
    {
        get => _elapsedTimeText;
        private set => SetField(ref _elapsedTimeText, value);
    }

    private string _errorText = string.Empty;
    public string ErrorText
    {
        get => _errorText;
        private set => SetField(ref _errorText, value);
    }

    public bool IsDebugSelected => SelectedConfiguration == BuildConfigurationEnum.Debug;

    public bool IsReleaseSelected => SelectedConfiguration == BuildConfigurationEnum.Release;

#pragma warning disable CS8618
    public BuildProjectWindowViewModel() { }
#pragma warning restore CS8618

    public BuildProjectWindowViewModel(
        IProjectBuildService projectBuildService,
        IProjectBuildWindowService windowService,
        ProjectBuildOutputPathUtility outputPathUtility,
        IStorageProvider storageProvider)
    {
        _projectBuildService = projectBuildService;
        _windowService = windowService;
        _outputPathUtility = outputPathUtility;
        _storageProvider = storageProvider;

        OutputPath = _outputPathUtility.GetDefaultPackageOutputPath(SelectedConfiguration);

        BuildCommand = new RelayCommand(StartBuildAsync, () => !IsBuildInProgress);
        CancelCommand = new RelayCommand(CancelOrClose);
        SelectOutputPathCommand = new RelayCommand(SelectOutputPathAsync);
        SelectIconPathCommand = new RelayCommand(SelectIconPathAsync);
        SelectDebugConfigurationCommand = new RelayCommand(SelectDebugConfiguration);
        SelectReleaseConfigurationCommand = new RelayCommand(SelectReleaseConfiguration);
    }

    public override void Dispose()
    {
        base.Dispose();
        StopElapsedTimer();
        _buildCancellationTokenSource?.Dispose();
        _buildCancellationTokenSource = null;
    }

    private async void StartBuildAsync()
    {
        if (IsBuildInProgress) return;
        if (!await ConfirmOutputDirectoryDeletionAsync()) return;

        ProjectBuildResult result = default;
        var hasResult = false;
        try
        {
            ErrorText = string.Empty;
            IsCancelRequested = false;
            IsBuildInProgress = true;
            ProgressStatus = "Starting build...";
            ProgressValue = 0;
            StartElapsedTimer();

            _buildCancellationTokenSource = new CancellationTokenSource();
            var request = new ProjectBuildRequest(
                SelectedConfiguration,
                OutputPath,
                ShowConsole,
                IconPath);

            result = await Task.Run(
                () => _projectBuildService.BuildAsync(
                    request,
                    progress => Dispatcher.UIThread.Post(() =>
                    {
                        ProgressStatus = progress.Status;
                        ProgressValue = progress.ProgressValue;
                    }),
                    _buildCancellationTokenSource.Token),
                _buildCancellationTokenSource.Token);
            hasResult = true;
        }
        catch (OperationCanceledException)
        {
            result = new ProjectBuildResult(false, true, "Build canceled.");
            hasResult = true;
        }
        catch (Exception e)
        {
            ErrorText = e.Message;
            ProgressStatus = "Build failed";
        }
        finally
        {
            StopElapsedTimer();
            IsBuildInProgress = false;
            IsCancelRequested = false;
            _buildCancellationTokenSource?.Dispose();
            _buildCancellationTokenSource = null;
        }

        if (!hasResult) return;

        if (result.IsSuccess)
        {
            _windowService.CloseWindow();
            return;
        }

        ErrorText = result.ErrorMessage;
        ProgressStatus = "Build failed";
        if (result.IsCancelled)
        {
            ProgressStatus = "Build canceled";
        }
    }

    private void CancelOrClose()
    {
        if (!IsBuildInProgress)
        {
            _windowService.CloseWindow();
            return;
        }

        if (IsCancelRequested) return;
        IsCancelRequested = true;
        ProgressStatus = "Cancel requested. Waiting for current step to finish...";
        _buildCancellationTokenSource?.Cancel();
    }

    private async void SelectOutputPathAsync()
    {
        if (IsBuildInProgress) return;

        var folders = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select build output folder",
            AllowMultiple = false,
        });

        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        OutputPath = Path.GetFullPath(path);
    }

    private async void SelectIconPathAsync()
    {
        if (IsBuildInProgress) return;

        var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select .ico file",
            AllowMultiple = false,
            FileTypeFilter = new[] { FileExtensions.GetFilePicker(".ico") },
        });

        if (files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        IconPath = Path.GetFullPath(path);
    }

    private void SelectDebugConfiguration()
    {
        SelectedConfiguration = BuildConfigurationEnum.Debug;
    }

    private void SelectReleaseConfiguration()
    {
        SelectedConfiguration = BuildConfigurationEnum.Release;
    }

    private async Task<bool> ConfirmOutputDirectoryDeletionAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            ErrorText = "Output path is required.";
            return false;
        }

        var fullOutputPath = Path.GetFullPath(OutputPath);
        if (!Directory.Exists(fullOutputPath)) return true;
        if (!Directory.EnumerateFileSystemEntries(fullOutputPath).Any()) return true;

        var prompt = MessageBoxManager.GetMessageBoxStandard(
            "Output Folder",
            "Output folder is not empty. Delete all contents and continue?",
            ButtonEnum.YesNo);
        var result = await prompt.ShowAsync();
        if (result == ButtonResult.Yes) return true;

        ErrorText = "Build canceled. Select an empty output folder or confirm deletion.";
        return false;
    }

    private void StartElapsedTimer()
    {
        _buildStartedAtUtc = DateTime.UtcNow;
        _elapsedTimer?.Stop();
        _elapsedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _elapsedTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - _buildStartedAtUtc;
            ElapsedTimeText = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
        };
        _elapsedTimer.Start();
    }

    private void StopElapsedTimer()
    {
        if (_elapsedTimer == null) return;
        _elapsedTimer.Stop();
        _elapsedTimer = null;
    }
}
