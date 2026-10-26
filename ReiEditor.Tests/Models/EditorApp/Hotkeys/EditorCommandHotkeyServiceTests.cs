using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.EditorApp.Hotkeys;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.EditorApp.Hotkeys;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "EditorCommands")]
public sealed class EditorCommandHotkeyServiceTests
{
    private sealed class TestWindow(Control child) : Window
    {
        public void AttachChild() => VisualChildren.Add(child);
    }

    private sealed class Windows(Window window) : IMainWindowService
    {
        public event Action? ActivatedEvent;
        public event Action? DeactivatedEvent { add { } remove { } }
        public Window Window { get; set; } = window;
        public Window GetMainWindow() => Window;
        public void ShowMainWindow(Window value) => Window = value;
        public void ShowDialog(Window value) => throw new NotSupportedException();
        public void Activate() => ActivatedEvent?.Invoke();
    }

    private sealed class Input : IEngineInputService
    {
        public event Action<EngineEditorInputEvent>? InputReceivedEvent;
        public int Subscribers => InputReceivedEvent?.GetInvocationList().Length ?? 0;
        public void SubscribeToClient() => throw new NotSupportedException();
        public void Send(int key, int mods, EngineEditorInputEventType type = EngineEditorInputEventType.KeyDown) => InputReceivedEvent?.Invoke(new() { Type = type, Code = key, Mods = mods });
    }

    private sealed class Commands : IEditorCommandService
    {
        public bool IsBusy => false;
        public bool CanUndo { get; set; } = true;
        public bool CanRedo { get; set; } = true;
        public int UndoCalls { get; private set; }
        public int RedoCalls { get; private set; }
        public Task UndoResult { get; set; } = Task.CompletedTask;
        public Task<TResult> ExecuteAsync<TArgs, TResult>(IEditorCommand<TArgs, TResult> command, TArgs args) => throw new NotSupportedException();
        public Task UndoAsync() { UndoCalls++; return UndoResult; }
        public Task RedoAsync() { RedoCalls++; return Task.CompletedTask; }
    }

    [AvaloniaFact]
    public void CtrlZAndCtrlYUseServiceAndTextEditingIsExcluded()
    {
        var text = new TextBox();
        var window = new TestWindow(text);
        window.AttachChild();
        var commands = new Commands();
        using var service = new EditorCommandHotkeyService(new Windows(window), commands, new Input(), new TestEngineRunner(), new TestLogger<EditorCommandHotkeyService>());
        Assert.True(RaiseKey(window, Key.Z, KeyModifiers.Control).Handled);
        Assert.True(RaiseKey(window, Key.Y, KeyModifiers.Control).Handled);
        RaiseKey(text, Key.Z, KeyModifiers.Control);
        RaiseKey(text, Key.Y, KeyModifiers.Control);
        Assert.False(RaiseKey(window, Key.Z, KeyModifiers.Control | KeyModifiers.Alt).Handled);
        Assert.False(RaiseKey(window, Key.Z).Handled);
        Assert.Equal(1, commands.UndoCalls);
        Assert.Equal(1, commands.RedoCalls);
        commands.CanUndo = false;
        RaiseKey(window, Key.Z, KeyModifiers.Control);
        Assert.Equal(1, commands.UndoCalls);
    }

    [AvaloniaFact]
    public void ActivationMovesHandlerAndDisposeRemovesWindowAndNativeSubscriptions()
    {
        var oldWindow = new Window();
        var windows = new Windows(oldWindow);
        var commands = new Commands();
        var input = new Input();
        var runner = new TestEngineRunner();
        runner.EditorActive.Value = true;
        var service = new EditorCommandHotkeyService(windows, commands, input, runner, new TestLogger<EditorCommandHotkeyService>());
        windows.Activate();
        RaiseKey(oldWindow, Key.Z, KeyModifiers.Control);
        windows.Window = new Window();
        windows.Activate();
        RaiseKey(oldWindow, Key.Z, KeyModifiers.Control);
        RaiseKey(windows.Window, Key.Y, KeyModifiers.Control);
        Assert.Equal(1, commands.UndoCalls);
        Assert.Equal(1, commands.RedoCalls);
        Assert.Equal(1, input.Subscribers);
        service.Dispose();
        windows.Activate();
        RaiseKey(windows.Window, Key.Y, KeyModifiers.Control);
        input.Send(EngineInputConstants.KEY_Z, EngineInputConstants.MOD_CONTROL);
        Assert.Equal(0, input.Subscribers);
        Assert.Equal(1, commands.UndoCalls);
        Assert.Equal(1, commands.RedoCalls);
    }

    [AvaloniaFact]
    public void NativeViewportKeysRespectEditorModeModifiersAndKeyDown()
    {
        var commands = new Commands();
        var input = new Input();
        var runner = new TestEngineRunner();
        using var service = new EditorCommandHotkeyService(new Windows(new Window()), commands, input, runner, new TestLogger<EditorCommandHotkeyService>());
        input.Send(EngineInputConstants.KEY_Z, EngineInputConstants.MOD_CONTROL);
        Assert.Equal(0, commands.UndoCalls);
        runner.EditorActive.Value = true;
        input.Send(EngineInputConstants.KEY_Z, EngineInputConstants.MOD_CONTROL | EngineInputConstants.MOD_CAPS_LOCK);
        input.Send(EngineInputConstants.KEY_Y, EngineInputConstants.MOD_CONTROL);
        input.Send(EngineInputConstants.KEY_Z, EngineInputConstants.MOD_CONTROL | EngineInputConstants.MOD_ALT);
        input.Send(EngineInputConstants.KEY_Z, EngineInputConstants.MOD_CONTROL, EngineEditorInputEventType.KeyUp);
        Assert.Equal(1, commands.UndoCalls);
        Assert.Equal(1, commands.RedoCalls);
    }

    [AvaloniaFact]
    public void AsyncCommandErrorsAreLogged()
    {
        var error = new InvalidOperationException("undo failed");
        var commands = new Commands { UndoResult = Task.FromException(error) };
        var window = new Window();
        var logger = new TestLogger<EditorCommandHotkeyService>();
        using var service = new EditorCommandHotkeyService(new Windows(window), commands, new Input(), new TestEngineRunner(), logger);
        RaiseKey(window, Key.Z, KeyModifiers.Control);
        Assert.Same(error, Assert.Single(logger.Entries).Exception);
    }

    private static KeyEventArgs RaiseKey(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
        target.RaiseEvent(args);
        return args;
    }
}
