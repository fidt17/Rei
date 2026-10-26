using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Hotkeys;

public sealed class EditorCommandHotkeyService : IDisposable
{
    private readonly IMainWindowService _windows;
    private readonly IEditorCommandService _commands;
    private readonly IEngineInputService _input;
    private readonly IEngineRunner _engine;
    private readonly ILogger<EditorCommandHotkeyService> _logger;
    private Window? _window;

    public EditorCommandHotkeyService(IMainWindowService windows, IEditorCommandService commands, IEngineInputService input, IEngineRunner engine, ILogger<EditorCommandHotkeyService> logger)
    {
        _windows = windows;
        _commands = commands;
        _input = input;
        _engine = engine;
        _logger = logger;
        _windows.ActivatedEvent += Attach;
        _input.InputReceivedEvent += NativeInput;
        Attach();
    }

    public void Dispose()
    {
        _windows.ActivatedEvent -= Attach;
        _input.InputReceivedEvent -= NativeInput;
        _window?.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
    }

    private void Attach()
    {
        Window window;
        try { window = _windows.GetMainWindow(); }
        catch { return; }
        if (ReferenceEquals(window, _window)) return;
        _window?.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
        _window = window;
        _window.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Bubble);
    }

    private void KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Handled || args.Source is TextBox || _window?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (args.KeyModifiers != KeyModifiers.Control || args.Key is not (Key.Z or Key.Y)) return;
        args.Handled = true;
        _ = TraverseAsync(args.Key == Key.Z);
    }

    private void NativeInput(EngineEditorInputEvent input)
    {
        if (!_engine.IsEditorActive.Value || input.Type != EngineEditorInputEventType.KeyDown) return;
        var modifiers = input.Mods & (EngineInputConstants.MOD_CONTROL | EngineInputConstants.MOD_SHIFT | EngineInputConstants.MOD_ALT | EngineInputConstants.MOD_SUPER);
        if (modifiers != EngineInputConstants.MOD_CONTROL || input.Code is not (EngineInputConstants.KEY_Z or EngineInputConstants.KEY_Y)) return;
        _ = TraverseAsync(input.Code == EngineInputConstants.KEY_Z);
    }

    private async Task TraverseAsync(bool undo)
    {
        if (!(undo ? _commands.CanUndo : _commands.CanRedo)) return;
        try
        {
            if (undo) await _commands.UndoAsync();
            else await _commands.RedoAsync();
        }
        catch (Exception error) { _logger.LogException(error); }
    }
}
