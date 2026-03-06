using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.EditorApp.Selection;

public interface IEntitySelectable : ISelectable
{
    GameEntity Entity { get; }
}