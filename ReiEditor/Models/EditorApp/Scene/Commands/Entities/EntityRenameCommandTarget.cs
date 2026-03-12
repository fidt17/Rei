using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public sealed record EntityRenameCommandTarget(GameEntity Entity, string Name);
