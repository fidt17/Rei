using System.Runtime.InteropServices;

namespace ReiEditor.Models.Services.Engine.Api.DTO;

[StructLayout(LayoutKind.Sequential)]
public struct EngineEditorInputEvent
{
    public EngineEditorInputEventType Type;
    public int Code;
    public int Mods;
    public float MouseX;
    public float MouseY;
}
