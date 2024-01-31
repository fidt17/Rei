using System.Threading.Tasks;

namespace ReiEditor.Models.Resources.Client;

public interface IResourceService
{
	string GetRootPath();
	string GetProjectPath(params string[] path);
	string GetSolutionPath(params string[] path);
	
	Task<T?> Load<T>(string fullPath);
	Task<bool> Write(string data, string fullPath);
	bool Exists(string fullPath);
}