namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public sealed record ProjectAssetCommandTarget(
    string FullPath,
    bool IsDirectory);
