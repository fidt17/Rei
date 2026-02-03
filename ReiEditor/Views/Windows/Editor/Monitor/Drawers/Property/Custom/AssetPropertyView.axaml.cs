using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property.Custom;

public partial class AssetPropertyView : UserControl
{
    private AssetPropertyViewModel? _viewModel;

    public AssetPropertyView()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
        AddHandler(KeyDownEvent, AssetPropertyView_OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragEnterEvent, AssetPropertyView_OnDragEnter);
        AddHandler(DragDrop.DropEvent, AssetPropertyView_OnDrop);
    }

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.AssetSelectedEvent -= HandleAssetSelectedEvent;
        }

        _viewModel = DataContext as AssetPropertyViewModel;

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
        if (_viewModel != null)
        {
            _viewModel.SearchField.ResetSearch();
            _viewModel.RefreshSearchResultsForAll();
        }
        SearchFieldControl.FocusInput();
    }

    private void AssetNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Delete) return;
        if (_viewModel == null) return;

        _viewModel.ClearAsset();
        e.Handled = true;
    }

    private void AssetPropertyView_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Delete) return;
        if (_viewModel == null) return;
        if (!AssetNameTextBox.IsKeyboardFocusWithin) return;

        _viewModel.ClearAsset();
        e.Handled = true;
    }

    private void AssetNameTextBox_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        AssetNameTextBox.Focus();
    }

    private void AssetPropertyView_OnDragEnter(object? sender, DragEventArgs e)
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

    private void AssetPropertyView_OnDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel == null) return;
        if (!TryGetAssetPath(e, out var assetPath)) return;

        if (_viewModel.TryAssignAssetFromPath(assetPath))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
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
