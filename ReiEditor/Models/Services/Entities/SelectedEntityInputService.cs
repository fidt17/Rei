using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Entities;

public class SelectedEntityInputService
{
    private readonly record struct InputBinding(int KeyCode, int Mods = 0)
    {
        public const int MODIFIER_MASK =
            EngineInputConstants.MOD_SHIFT |
            EngineInputConstants.MOD_CONTROL |
            EngineInputConstants.MOD_ALT |
            EngineInputConstants.MOD_SUPER |
            EngineInputConstants.MOD_CAPS_LOCK |
            EngineInputConstants.MOD_NUM_LOCK;
    }
    
    private readonly ISelectedEntityActionService _selectedEntityActionService;
    private readonly IEngineRunner _engineRunner;
    private readonly IReadOnlyDictionary<InputBinding, Func<bool>> _keyDownActions;

    public SelectedEntityInputService(
        ISelectedEntityActionService selectedEntityActionService,
        IEngineInputService engineInputService,
        IEngineRunner engineRunner)
    {
        _selectedEntityActionService = selectedEntityActionService;
        _engineRunner = engineRunner;

        _keyDownActions = new Dictionary<InputBinding, Func<bool>>
        {
            [new InputBinding(EngineInputConstants.KEY_DELETE)] = _selectedEntityActionService.DeleteSelectedEntity,
            [new InputBinding(EngineInputConstants.KEY_D, EngineInputConstants.MOD_CONTROL)] = _selectedEntityActionService.DuplicateSelectedEntity,
        };

        engineInputService.InputReceivedEvent += HandleEngineInputEvent;
    }

    private void HandleEngineInputEvent(EngineEditorInputEvent inputEvent)
    {
        if (!_engineRunner.IsEditorActive.Value) return;
        if (inputEvent.Type != EngineEditorInputEventType.KeyDown) return;

        var binding = new InputBinding(inputEvent.Code, inputEvent.Mods & InputBinding.MODIFIER_MASK);
        if (!_keyDownActions.TryGetValue(binding, out var action)) return;

        action();
    }
}
