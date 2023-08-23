using System.Threading.Tasks;

namespace ReiEditor.Models.Resources.Client;

public interface IResourceService
{
	string GetFullPath(params string[] path);
	
	Task<string?> Load(string path);

	bool Copy(string from, string to, bool overrideContents);
}