using System.Collections.Generic;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public sealed record SelectedEntityCommandTarget(GameEntity PrimaryEntity, IReadOnlyList<GameEntity> Entities);
