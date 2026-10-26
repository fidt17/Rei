namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public sealed record EntityRenameCommandResult(string BeforeName, string AfterName)
{
    public bool Changed => BeforeName != AfterName;
}
