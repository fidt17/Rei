using System.Threading.Tasks;

namespace Editor.Startup.Common;

public interface IAsyncInitializable
{
	Task InitializeAsync();
}