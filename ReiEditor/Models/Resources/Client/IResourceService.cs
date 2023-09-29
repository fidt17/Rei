using System.Threading.Tasks;

namespace ReiEditor.Models.Resources.Client;

public interface IResourceService
{
	string GetFullPath(params string[] path);
	
	Task<string?> Load(string path);
	Task<bool> Write(string data, string path);
	bool Exists(string path);
}