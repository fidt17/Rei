namespace ReiEditor.Models.Services.Render;

public class RenderSettingsService : IRenderSettingsService
{
    public RenderMode RenderMode { get; set; } = RenderMode.Shaded;
    public bool IsUiRenderingEnabled { get; set; } = true;
}
