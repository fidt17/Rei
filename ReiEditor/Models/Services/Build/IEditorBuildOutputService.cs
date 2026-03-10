namespace ReiEditor.Models.Services.Build;

public interface IEditorBuildOutputService
{
    EditorBuildOutput GetLiveOutput();
    EditorBuildOutput PrepareStagingOutput();
    void PromoteStagingOutput(EditorBuildOutput stagingOutput);
    void CleanupStagingOutput();
}
