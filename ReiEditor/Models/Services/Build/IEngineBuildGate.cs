using System.Threading;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build;

public interface IEngineBuildGate
{
    Task StopEngineAndWaitForDllUnload(CancellationToken cancellationToken);
}
