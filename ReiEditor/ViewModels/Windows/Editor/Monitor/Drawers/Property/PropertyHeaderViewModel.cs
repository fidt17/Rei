using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public sealed class PropertyHeaderViewModel(string text) : BaseViewModel
{
    public string Text { get; } = text;
}
