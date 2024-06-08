using System;

namespace ReiEditor.Models.Services.Windows.Playmode;

public interface IPlaymodeWindowController
{
    Utils.Common.IObservable<IntPtr?> WindowPointer { get; }
    
    void SetupWindow();
    void DestroyWindow();
}