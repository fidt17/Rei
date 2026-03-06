namespace ReiEditor.Models.Services.FileSystem;

public interface ITextEditorFileOpener
{
    bool CanOpenWithTextEditor(string filePath);
    TextEditorOpenResult Open(string filePath);
}
