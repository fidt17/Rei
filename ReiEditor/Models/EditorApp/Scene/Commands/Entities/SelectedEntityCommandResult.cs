using System.Collections.Generic;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public sealed record SelectedEntityCommandResult(IReadOnlyList<int>? SelectedEntityIds = null, int? RenameEntityId = null);
