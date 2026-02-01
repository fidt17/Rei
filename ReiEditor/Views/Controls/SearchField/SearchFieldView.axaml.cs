using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.Views.Controls.SearchField;

public partial class SearchFieldView : UserControl
{
    public SearchFieldView()
    {
        InitializeComponent();
    }

    public void FocusInput()
    {
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private void SearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (DataContext is not SearchFieldViewModel vm) return;

        vm.ClearCommand.Execute(null);
        Focus();
        e.Handled = true;
    }

    private void SearchTextBox_OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (DataContext is not SearchFieldViewModel vm) return;
        vm.SetFocused(true);
    }

    private void SearchTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SearchFieldViewModel vm) return;
        vm.SetFocused(false);
    }
}
