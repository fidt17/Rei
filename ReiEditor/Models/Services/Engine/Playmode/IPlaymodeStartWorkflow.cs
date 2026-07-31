using System.Threading;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IPlaymodeStartWorkflow
{
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
}
