using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Resources;

public interface IResourceLoader
{
	Task<string?> Load(params string[] path);
}