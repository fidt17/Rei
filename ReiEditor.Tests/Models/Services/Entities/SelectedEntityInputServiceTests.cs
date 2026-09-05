using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Entities;

/// <summary>
/// Verifies keyboard input dispatch for selected entity actions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Entities")]
public sealed class SelectedEntityInputServiceTests
{
    private sealed class TestActionService : ISelectedEntityActionService
    {
        public event Action<int>? RenameEntityRequested;
        public int DeleteCalls { get; private set; }
        public int DuplicateCalls { get; private set; }

        public bool DeleteSelectedEntity()
        {
            DeleteCalls++;
            return true;
        }

        public bool DuplicateSelectedEntity()
        {
            DuplicateCalls++;
            return true;
        }

        public bool RequestRenameSelectedEntity()
        {
            RenameEntityRequested?.Invoke(0);
            return true;
        }
    }

    private sealed class TestInputService : IEngineInputService
    {
        public event Action<EngineEditorInputEvent>? InputReceivedEvent;
        public void SubscribeToClient() => throw new NotSupportedException();
        public void Raise(EngineEditorInputEvent inputEvent) => InputReceivedEvent?.Invoke(inputEvent);
    }

    /// <summary>
    /// Active editor Delete key down dispatches delete action.
    /// </summary>
    [Fact]
    public void TestDeleteKeyDownDispatchesDeleteWhenEditorActive()
    {
        var (actions, input, runner) = CreateService();
        runner.EditorActive.Value = true;

        input.Raise(KeyEvent(EngineEditorInputEventType.KeyDown, EngineInputConstants.KEY_DELETE));

        Assert.Equal(1, actions.DeleteCalls);
        Assert.Equal(0, actions.DuplicateCalls);
    }

    /// <summary>
    /// Active editor Control+D key down dispatches duplicate action.
    /// </summary>
    [Fact]
    public void TestControlDKeyDownDispatchesDuplicateWhenEditorActive()
    {
        var (actions, input, runner) = CreateService();
        runner.EditorActive.Value = true;

        input.Raise(KeyEvent(
            EngineEditorInputEventType.KeyDown,
            EngineInputConstants.KEY_D,
            EngineInputConstants.MOD_CONTROL));

        Assert.Equal(0, actions.DeleteCalls);
        Assert.Equal(1, actions.DuplicateCalls);
    }

    /// <summary>
    /// Unknown modifier bits are ignored when matching a supported shortcut.
    /// </summary>
    [Fact]
    public void TestUnknownModifierBitsDoNotBlockSupportedShortcut()
    {
        const int UNKNOWN_MODIFIER = 0x1000;
        var (actions, input, runner) = CreateService();
        runner.EditorActive.Value = true;

        input.Raise(KeyEvent(
            EngineEditorInputEventType.KeyDown,
            EngineInputConstants.KEY_D,
            EngineInputConstants.MOD_CONTROL | UNKNOWN_MODIFIER));

        Assert.Equal(1, actions.DuplicateCalls);
    }

    /// <summary>
    /// Inactive editor, key-up, unknown keys, and mismatched known modifiers do not dispatch actions.
    /// </summary>
    [Fact]
    public void TestUnsupportedInputDoesNotDispatchActions()
    {
        var (actions, input, runner) = CreateService();

        input.Raise(KeyEvent(EngineEditorInputEventType.KeyDown, EngineInputConstants.KEY_DELETE));
        runner.EditorActive.Value = true;
        input.Raise(KeyEvent(EngineEditorInputEventType.KeyUp, EngineInputConstants.KEY_DELETE));
        input.Raise(KeyEvent(EngineEditorInputEventType.KeyDown, EngineInputConstants.KEY_A));
        input.Raise(KeyEvent(
            EngineEditorInputEventType.KeyDown,
            EngineInputConstants.KEY_DELETE,
            EngineInputConstants.MOD_SHIFT));
        input.Raise(KeyEvent(
            EngineEditorInputEventType.KeyDown,
            EngineInputConstants.KEY_D,
            EngineInputConstants.MOD_CONTROL | EngineInputConstants.MOD_ALT));

        Assert.Equal(0, actions.DeleteCalls);
        Assert.Equal(0, actions.DuplicateCalls);
    }

    private static (TestActionService Actions, TestInputService Input, TestEngineRunner Runner) CreateService()
    {
        var actions = new TestActionService();
        var input = new TestInputService();
        var runner = new TestEngineRunner();
        _ = new SelectedEntityInputService(actions, input, runner);
        return (actions, input, runner);
    }

    private static EngineEditorInputEvent KeyEvent(EngineEditorInputEventType type, int code, int mods = 0)
    {
        return new EngineEditorInputEvent
        {
            Type = type,
            Code = code,
            Mods = mods,
        };
    }
}
