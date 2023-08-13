using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Storage;

public interface IEditorStorageService
{
	Task<bool> WriteToFile(string fileName, string value);
	Task<string?> ReadFromFile(string fileName);
}