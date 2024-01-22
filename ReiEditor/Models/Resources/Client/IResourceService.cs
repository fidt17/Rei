using System.Threading.Tasks;

namespace ReiEditor.Models.Resources.Client;

public interface IResourceService
{
	string GetFullPath(params string[] path);
	string GetSolutionPath(params string[] path);
	
	Task<T?> Load<T>(string path);
	Task<bool> Write(string data, string path);
	bool Exists(string path);
}