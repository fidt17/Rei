using System;
using System.Windows.Input;
using Avalonia.Media;
using ReactiveUI;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Path;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Assets;

public class ProjectAssetItemViewModel : BaseViewModel
{
    public ICommand SelectCommand { get; }
    public RelayCommand StartRenameCommand { get; }
    public RelayCommand ConfirmRenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand MoveCommand { get; }
    public ICommand OpenCommand { get; }

    public ObservableField<string> Name { get; }
    public ObservableField<string> RenameValue { get; } = new("");
    public ObservableField<bool> Selected { get; } = new(false);

    public string FullPath { get; }
    public ProjectAssetType AssetType { get; }
    public bool IsDirectory { get; }
    public IImage Icon { get; }

    public ContextMenuViewModel ContextMenu { get; } = new();
    public ContextMenuViewModel CombinedContextMenu { get; } = new();

#pragma warning disable CS8618
    public ProjectAssetItemViewModel() { }
#pragma warning restore CS8618

    public ProjectAssetItemViewModel(
        string name,
        string fullPath,
        ProjectAssetType assetType,
        Action<ProjectAssetItemViewModel> deleteAction,
        Action<ProjectAssetItemViewModel> duplicateAction,
        Action<ProjectAssetItemViewModel, string> renameAction,
        Action<ProjectAssetItemViewModel> moveAction,
        Action<ProjectAssetItemViewModel> openAction,
        ContextMenuViewModel activeFolderContextMenu,
        IFileExplorerProvider fileExplorerProvider)
    {
        Name = new ObservableField<string>(name);
        FullPath = fullPath;
        AssetType = assetType;
        IsDirectory = assetType == ProjectAssetType.Directory;
        Icon = ProjectAssetIconProvider.GetAssetIcon(assetType);
        
        SelectCommand = ReactiveCommand.Create(Select);
        StartRenameCommand = new RelayCommand(StartRename);
        ConfirmRenameCommand = new RelayCommand(() => ConfirmRename(renameAction));
        DeleteCommand = ReactiveCommand.Create(() => deleteAction(this));
        DuplicateCommand = ReactiveCommand.Create(() => duplicateAction(this));
        MoveCommand = ReactiveCommand.Create(() => moveAction(this));
        OpenCommand = ReactiveCommand.Create(() => openAction(this));

        SetupContextMenu(fileExplorerProvider);
        SetupCombinedContextMenu(activeFolderContextMenu);
    }

    public void Select() => Selected.Value = true;
    public void Deselect() => Selected.Value = false;

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
