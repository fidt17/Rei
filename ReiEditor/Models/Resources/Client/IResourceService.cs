using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Resources.Client;

public interface IResourceService
{
	string GetRootPath(params string[] path);
	string GetProjectPath(params string[] path);
	string GetScriptsPath(params string[] path);

	IEnumerable<string> GetAllWithExtension(string extension);
	void CopyFilesRecursively(string source, string target);
	void MoveFilesRecursively(string source, string target);

	Task<T> Load<T>(string fullPath);
	Task<T?> TryLoad<T>(string fullPath);
	
	Task<bool> Write(string data, string fullPath);
	bool Exists(string fullPath);
}