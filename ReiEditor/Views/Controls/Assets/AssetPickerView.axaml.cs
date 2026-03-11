using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Controls.Assets;

namespace ReiEditor.Views.Controls.Assets;

public partial class AssetPickerView : UserControl
{
    private AssetPickerViewModel? _viewModel;

    public AssetPickerView()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
        AddHandler(KeyDownEvent, AssetPickerView_OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragEnterEvent, AssetPickerView_OnDragEnter);
        AddHandler(DragDrop.DropEvent, AssetPickerView_OnDrop);
    }

    public void FocusInput()
    {
        SearchFieldControl.FocusInput();
    }

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.AssetSelectedEvent -= HandleAssetSelectedEvent;
        }

        _viewModel = DataContext as AssetPickerViewModel;
        if (_viewModel != null)
        {
            _viewModel.AssetSelectedEvent += HandleAssetSelectedEvent;
        }
    }

    private void HandleAssetSelectedEvent()
    {
        SelectButton.Flyout?.Hide();
    }

    private void SearchFlyout_OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not AssetPickerViewModel viewModel) return;
        viewModel.SearchField.ResetSearch();
        viewModel.RefreshSearchResultsForAll();
        FocusInput();
    }

    private void LabelBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        LabelBorder.Focus();
        if (_viewModel == null) return;

        if (_viewModel.HasActiveAsset && e.ClickCount >= 2)
        {
            _viewModel.ActivateAsset();
            e.Handled = true;
            return;
        }

        if (!_viewModel.HasActiveAsset)
        {
            OpenSearchFlyout();
        }

        e.Handled = true;
    }

    private void LabelBorder_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        if (e.Key is Key.Delete)
        {
            _viewModel.ClearAsset();
            e.Handled = true;
            return;
        }

        if (e.Key is not Key.Enter and not Key.Space) return;

        if (_viewModel.HasActiveAsset)
        {
            _viewModel.ActivateAsset();
            e.Handled = true;
            return;
        }

        OpenSearchFlyout();
        e.Handled = true;
    }

    private void AssetPickerView_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Delete) return;
        if (_viewModel == null) return;

        _viewModel.ClearAsset();
        e.Handled = true;
    }

    private void AssetPickerView_OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_viewModel == null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (!TryGetAssetPath(e, out var assetPath) || !_viewModel.CanAcceptAssetPath(assetPath))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void AssetPickerView_OnDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel == null) return;
        if (!TryGetAssetPath(e, out var assetPath)) return;

        if (_viewModel.TryAssignAssetFromPath(assetPath))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OpenSearchFlyout()
    {
        if (_viewModel == null) return;
        if (!_viewModel.IsSelectionSupported) return;

        SelectButton.Flyout?.ShowAt(SelectButton);
    }

    private static bool TryGetAssetPath(DragEventArgs e, out string path)
    {
        path = string.Empty;
        if (!e.Data.Contains(DragDropDataKeys.AssetPath)) return false;
        if (e.Data.Get(DragDropDataKeys.AssetPath) is not string assetPath) return false;

        path = assetPath;
        return true;
    }
}
