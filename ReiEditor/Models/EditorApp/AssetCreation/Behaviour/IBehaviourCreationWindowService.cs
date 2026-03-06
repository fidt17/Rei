using System;

namespace ReiEditor.Models.EditorApp.AssetCreation.Behaviour;

public interface IBehaviourCreationWindowService
{
    void OpenBehaviourCreationWindow(string targetDirectory, Action onCreated);
    void CloseBehaviourCreationWindow();
}
