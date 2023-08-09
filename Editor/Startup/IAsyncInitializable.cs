using System.Threading.Tasks;

namespace Editor.Startup;

public interface IAsyncInitializable
{
	Task InitializeAsync();
}