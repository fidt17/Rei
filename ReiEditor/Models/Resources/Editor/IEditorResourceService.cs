using System.Threading.Tasks;

namespace ReiEditor.Models.Resources.Editor;

public interface IEditorResourceService
{
	Task<string?> Load(params string[] path);
}