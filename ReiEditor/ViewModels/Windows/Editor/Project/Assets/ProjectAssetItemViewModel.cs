using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using ReactiveUI;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Path;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Assets;

public class ProjectAssetItemViewModel : BaseViewModel, IAssetSelectable
{
    public RelayCommand StartRenameCommand { get; }
    public RelayCommand ConfirmRenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand MoveCommand { get; }
    public ICommand OpenCommand { get; }

    public ObservableField<string> Name { get; }
    public ObservableField<string> RenameValue { get; } = new("");
    public ObservableField<bool> Selected { get; } = new(false);
    public ObservableField<bool> Highlighted { get; } = new(false);

    public string FullPath { get; }
    public ProjectAssetType AssetType { get; }
    public bool IsDirectory { get; }
    public IImage Icon { get; }
    public string AssetId { get; }
    public string AssetName => Name.Value;
    public string AssetPath => FullPath;
    public bool IsAssetSupportedInMonitor { get; }

    public ContextMenuViewModel ContextMenu { get; } = new();
    public ContextMenuViewModel CombinedContextMenu { get; } = new();

    private readonly Action<ProjectAssetItemViewModel, KeyModifiers>? _selectAction;
    private readonly Action<ProjectAssetItemViewModel>? _contextMenuSelectAction;
    private CancellationTokenSource? _highlightCTS;

#pragma warning disable CS8618
    public ProjectAssetItemViewModel() { }
#pragma warning restore CS8618

    public ProjectAssetItemViewModel(
        string name,
        string fullPath,
        ProjectAssetType assetType,
        string assetId,
        ProjectAssetItemActions actions,
        ContextMenuViewModel activeFolderContextMenu,
        IFileExplorerProvider fileExplorerProvider)
    {
        Name = new ObservableField<string>(name);
        FullPath = fullPath;
        AssetType = assetType;
        AssetId = assetId;
        IsDirectory = assetType == ProjectAssetType.Directory;
        IsAssetSupportedInMonitor = AssetMonitorSupportUtility.IsAssetSupportedInMonitor(fullPath, IsDirectory);
        Icon = ProjectAssetIconProvider.GetAssetIcon(assetType);
        _selectAction = actions.SelectAction;
        _contextMenuSelectAction = actions.ContextMenuSelectAction;

        StartRenameCommand = new RelayCommand(StartRename);
        ConfirmRenameCommand = new RelayCommand(() => ConfirmRename(actions.RenameAction));
        DeleteCommand = ReactiveCommand.Create(() => actions.DeleteAction(this));
        DuplicateCommand = ReactiveCommand.Create(() => actions.DuplicateAction(this));
        MoveCommand = ReactiveCommand.Create(() => actions.MoveAction(this));
        OpenCommand = ReactiveCommand.Create(() => actions.OpenAction(this));

        SetupContextMenu(fileExplorerProvider);
        SetupCombinedContextMenu(activeFolderContextMenu);
    }

    public override void Dispose()
    {
        base.Dispose();
        CancelHighlightPulse();
    }

    public void RequestSelection(KeyModifiers modifiers) => _selectAction?.Invoke(this, modifiers);
    public void RequestContextMenuSelection() => _contextMenuSelectAction?.Invoke(this);

    public void Select() => Selected.Value = true;
    public void Deselect() => Selected.Value = false;
    public void SetSelected(bool selected) => Selected.Value = selected;

    public void Highlight()
    {
        Highlighted.Value = true;
    }

    public void ClearHighlight()
    {
        CancelHighlightPulse();
        Highlighted.Value = false;
    }

    public void PulseHighlight(TimeSpan duration)
    {
        CancelHighlightPulse();
        _highlightCTS = new CancellationTokenSource();
        var token = _highlightCTS.Token;

        _ = RunHighlightPulse(duration, token);
    }

    private async Task RunHighlightPulse(TimeSpan duration, CancellationToken token)
    {
        Highlighted.Value = true;

        try
        {
            await Task.Delay(duration, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested) return;
        Highlighted.Value = false;
    }

    private void CancelHighlightPulse()
    {
        _highlightCTS?.Cancel();
        _highlightCTS?.Dispose();
        _highlightCTS = null;
    }

    private void StartRename() => RenameValue.Value = PathNamingUtils.GetRenameValue(Name.Value, IsDirectory);

    private void ConfirmRename(Action<ProjectAssetItemViewModel, string> renameAction)
    {
        var newName = RenameValue.Value.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;

        renameAction(this, PathNamingUtils.GetRenamedName(Name.Value, newName, IsDirectory));
    }

    private void SetupContextMenu(IFileExplorerProvider fileExplorerProvider)
    {
        ContextMenu.AddOption(new ContextMenuOption("Show in Explorer", () => fileExplorerProvider.OpenAndSelect(FullPath)));
        ContextMenu.AddOption(new ContextMenuOption("Rename", () => StartRenameCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuOption("Move", () => MoveCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuOption("Duplicate", () => DuplicateCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuOption("Delete", () => DeleteCommand.Execute(null)));
    }

    private void SetupCombinedContextMenu(ContextMenuViewModel activeFolderContextMenu)
    {
        var assetContextMenuClone = ContextMenu.Clone();
        foreach (var option in assetContextMenuClone.Options)
        {
            CombinedContextMenu.AddOption(option);
        }

        if (activeFolderContextMenu.Options.Count == 0) return;

        CombinedContextMenu.AddOption(ContextMenuOption.Separator());
        var activeFolderContextMenuClone = activeFolderContextMenu.Clone();
        foreach (var option in activeFolderContextMenuClone.Options)
        {
            CombinedContextMenu.AddOption(option);
        }
    }

}


