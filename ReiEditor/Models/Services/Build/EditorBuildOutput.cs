namespace ReiEditor.Models.Services.Build;

public sealed record EditorBuildOutput(
    string StagingRootPath,
    string BinDirectoryPath,
    string ClientOutputDirectoryPath,
    string ClientDllPath,
    string ResourcesDirectoryPath);
