namespace ReiEditor.Models.Services.Render;

public interface IRenderSettingsService
{
    RenderMode RenderMode { get; set; }
    bool IsUiRenderingEnabled { get; set; }
}
