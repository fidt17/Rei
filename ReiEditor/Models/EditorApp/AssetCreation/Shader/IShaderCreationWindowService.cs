using System;

namespace ReiEditor.Models.EditorApp.AssetCreation.Shader;

public interface IShaderCreationWindowService
{
    void OpenShaderCreationWindow(string targetDirectory, Action onCreated);
    void CloseShaderCreationWindow();
}
