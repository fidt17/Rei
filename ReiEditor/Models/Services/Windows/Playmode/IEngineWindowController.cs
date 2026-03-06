using System;

namespace ReiEditor.Models.Services.Windows.Playmode;

public interface IEngineWindowController
{
    Utils.Common.IObservable<IntPtr?> WindowPointer { get; }
    
    void SetupWindow();
    void DestroyWindow();
}