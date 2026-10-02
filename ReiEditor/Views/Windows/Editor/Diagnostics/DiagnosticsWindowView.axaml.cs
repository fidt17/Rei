using System;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReiEditor.ViewModels.Windows.Editor.Diagnostics;

namespace ReiEditor.Views.Windows.Editor.Diagnostics;

public partial class DiagnosticsWindowView : Window
{
    public DiagnosticsWindowView() => InitializeComponent();

    private async void ExportClicked(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not DiagnosticsWindowViewModel vm || vm.ExportJson() is not { } json) return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export profiling snapshot",
                SuggestedFileName = $"rei-profile-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                DefaultExtension = "json",
                FileTypeChoices = new[] { new FilePickerFileType("JSON snapshot") { Patterns = new[] { "*.json" } } }
            });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(json);
        }
        catch (Exception) { vm.ReportExportError(); }
    }
}
