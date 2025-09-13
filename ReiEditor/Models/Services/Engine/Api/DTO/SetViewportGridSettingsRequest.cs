using System.Runtime.InteropServices;
using ReiEditor.Models.EditorApp.ViewportGrid;

namespace ReiEditor.Models.Services.Engine.Api.DTO;

[StructLayout(LayoutKind.Sequential)]
public struct SetViewportGridSettingsRequest
{
    [MarshalAs(UnmanagedType.I1)] public bool RenderXZ;
    [MarshalAs(UnmanagedType.I1)] public bool RenderXY;
    [MarshalAs(UnmanagedType.I1)] public bool RenderYZ;
    public float Opacity;

    public SetViewportGridSettingsRequest(ViewportGridSettings s)
    {
        RenderXZ = s.RenderXZ;
        RenderXY = s.RenderXY;
        RenderYZ = s.RenderYZ;
        Opacity = s.Opacity;
    }
}