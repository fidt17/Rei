using System.Threading.Tasks;

namespace ReiEditor.Startup.Common;

public interface IAsyncInitializable
{
	Task InitializeAsync();
}