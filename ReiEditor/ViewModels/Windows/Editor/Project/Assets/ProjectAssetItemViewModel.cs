using System;
using System.Windows.Input;
using Avalonia.Media;
using ReactiveUI;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils;
using ReiEditor.Utils.Common;
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
    }

    public void Select() => Selected.Value = true;
    public void Deselect() => Selected.Value = false;

    private void StartRename() => RenameValue.Value = Name.Value;

    private void ConfirmRename(Action<ProjectAssetItemViewModel, string> renameAction)
    {
        var newName = RenameValue.Value;
        if (string.IsNullOrWhiteSpace(newName)) return;
        renameAction(this, newName);
    }

    private void SetupContextMenu(IFileExplorerProvider fileExplorerProvider)
    {
        ContextMenu.AddOption(new ContextMenuOption("Show in Explorer", () => fileExplorerProvider.OpenAndSelect(FullPath)));
        ContextMenu.AddOption(new ContextMenuOption("Rename", () => StartRenameCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuOption("Move", () => MoveCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuOption("Duplicate", () => DuplicateCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuOption("Delete", () => DeleteCommand.Execute(null)));
    }
}
