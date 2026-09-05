using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.Models.EditorApp.Hotkeys;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.TransformationControls;
using ReiEditor.Tests.Infrastructure.Headless;

namespace ReiEditor.Tests.Models.EditorApp.Hotkeys;

/// <summary>Verifies routed viewport transformation hotkeys and window subscription lifecycle.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Viewport")]
public sealed class EditorTransformationHotkeyServiceTests
{
    /// <summary>Attaches a child directly to the visual tree without requiring application themes.</summary>
    private sealed class TestWindow(Control child) : Window
    {
        public void AttachChild() => VisualChildren.Add(child);
    }

    /// <summary>Provides replaceable main windows and controlled activation events.</summary>
    private sealed class TestMainWindowService(Window window) : IMainWindowService
    {
        public event Action? ActivatedEvent;
        public event Action? DeactivatedEvent;
        public Window Window { get; set; } = window;
        public Window GetMainWindow() => Window;
        public void ShowMainWindow(Window value) => Window = value;
        public void ShowDialog(Window value) => throw new NotSupportedException();
        public void PublishActivated() => ActivatedEvent?.Invoke();
        public void PublishDeactivated() => DeactivatedEvent?.Invoke();
    }

    /// <summary>Records requested transformation modes.</summary>
    private sealed class TestTransformationControls : ITransformationControlsService
    {
        public event Action? StateChanged;
        public List<TransformationMode> Modes { get; } = new();
        public bool CanUseLocalSpace => true;
        public bool CanUseWorldSpace => true;
        public bool IsLocalSpace => false;
        public bool IsWorldSpace => true;
        public bool CanUseRectTransformMode => true;
        public bool EngineRunning => true;
        public TransformationMode Mode => Modes.LastOrDefault();
        public void SetWorldSpace() => throw new NotSupportedException();
        public void SetLocalSpace() => throw new NotSupportedException();
        public void SetMode(TransformationMode mode) => Modes.Add(mode);
        public void PublishStateChanged() => StateChanged?.Invoke();
    }

    /// <summary>W, E, R, and T route to movement, scale, rotation, and rect modes and mark events handled.</summary>
    [AvaloniaTheory]
    [InlineData(Key.W, TransformationMode.Movement)]
    [InlineData(Key.E, TransformationMode.Scale)]
    [InlineData(Key.R, TransformationMode.Rotation)]
    [InlineData(Key.T, TransformationMode.RectTransform)]
    public void TestSupportedKeysSetModeAndMarkHandled(Key key, TransformationMode expectedMode)
    {
        var window = new Window();
        var controls = new TestTransformationControls();
        using var service = new EditorTransformationHotkeyService(new TestMainWindowService(window), controls);

        var keyEvent = RaiseKey(window, key);

        Assert.True(keyEvent.Handled);
        Assert.Equal(expectedMode, Assert.Single(controls.Modes));
    }

    /// <summary>Modifiers, unrelated keys, and TextBox input do not trigger transformation commands.</summary>
    [AvaloniaFact]
    public void TestUnsupportedKeyInputDoesNotSetMode()
    {
        var textBox = new TextBox();
        var window = new TestWindow(textBox);
        window.AttachChild();
        var textRoutes = 0;
        window.AddHandler(InputElement.KeyDownEvent, (_, args) => { if (ReferenceEquals(args.Source, textBox)) textRoutes++; }, RoutingStrategies.Tunnel, handledEventsToo: true);
        var controls = new TestTransformationControls();
        using var service = new EditorTransformationHotkeyService(new TestMainWindowService(window), controls);

        var modified = RaiseKey(window, Key.W, KeyModifiers.Control);
        var unrelated = RaiseKey(window, Key.A);
        var textInput = RaiseKey(textBox, Key.W);

        Assert.False(modified.Handled);
        Assert.False(unrelated.Handled);
        Assert.False(textInput.Handled);
        Assert.Equal(1, textRoutes);
        Assert.Empty(controls.Modes);
    }

    /// <summary>Activation switches handler to replacement window without duplicating same-window subscriptions.</summary>
    [AvaloniaFact]
    public void TestActivationMovesHandlerToCurrentWindowWithoutDuplicates()
    {
        var oldWindow = new Window();
        var newWindow = new Window();
        var mainWindow = new TestMainWindowService(oldWindow);
        var controls = new TestTransformationControls();
        using var service = new EditorTransformationHotkeyService(mainWindow, controls);
        mainWindow.PublishActivated();
        RaiseKey(oldWindow, Key.W);
        mainWindow.Window = newWindow;
        mainWindow.PublishActivated();

        RaiseKey(oldWindow, Key.E);
        RaiseKey(newWindow, Key.R);

        Assert.Equal(new[] { TransformationMode.Movement, TransformationMode.Rotation }, controls.Modes);
    }

    /// <summary>Dispose removes window and activation handlers.</summary>
    [AvaloniaFact]
    public void TestDisposeDetachesAllHandlers()
    {
        var window = new Window();
        var mainWindow = new TestMainWindowService(window);
        var controls = new TestTransformationControls();
        var service = new EditorTransformationHotkeyService(mainWindow, controls);

        service.Dispose();
        RaiseKey(window, Key.W);
        mainWindow.PublishActivated();
        RaiseKey(window, Key.E);

        Assert.Empty(controls.Modes);
    }

    private static KeyEventArgs RaiseKey(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var keyEvent = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
        };
        target.RaiseEvent(keyEvent);
        return keyEvent;
    }
}
