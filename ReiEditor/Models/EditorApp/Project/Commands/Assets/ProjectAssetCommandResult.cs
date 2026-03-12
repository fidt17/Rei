namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public sealed record ProjectAssetCommandResult(
    bool AffectsTree,
    string? SelectedAssetPath = null);
