using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Resources.EngineResources;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public class ImportEngineResourcesCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;
	
    private readonly IEngineResourcesImporter _importer;
    private bool _isImporting;

    public ImportEngineResourcesCommand(IEngineResourcesImporter importer)
    {
        _importer = importer;
    }

    public bool CanExecute(object? parameter) => !_isImporting;

    public void Execute(object? parameter)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            _isImporting = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            
            try
            {
                await _importer.Import();
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
            }

            _isImporting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}