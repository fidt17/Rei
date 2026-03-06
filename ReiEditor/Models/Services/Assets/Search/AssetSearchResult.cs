namespace ReiEditor.Models.Services.Assets.Search;

public sealed class AssetSearchResult
{
    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }

    public AssetSearchResult(string name, string fullPath, bool isDirectory)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
    }
}
