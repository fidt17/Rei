namespace ReiEditor.Models.EditorApp.ViewportGrid;

public interface IViewportGridService
{
    ViewportGridSettings GetCurrentSettings();
    
    void EnableXZGrid(bool value);
    void EnableXYGrid(bool value);
    void EnableYZGrid(bool value);
    void SetOpacity(float value);
}