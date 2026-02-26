using System;

namespace ReiEditor.Models.EditorApp.AssetCreation.Material;

public interface IMaterialCreationWindowService
{
    void OpenMaterialCreationWindow(string targetDirectory, Action onCreated);
    void CloseMaterialCreationWindow();
}
