using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IEditorModeStarter
{
    ICondition CanStart { get; }
    
    void Start();
}