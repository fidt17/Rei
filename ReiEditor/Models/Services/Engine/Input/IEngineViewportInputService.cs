using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Engine.Input;

public interface IEngineViewportInputService
{
    Task<ReiViewportInputResult> SendAsync(IReadOnlyList<ReiViewportInputEvent> events, CancellationToken cancellationToken);
    Task<ReiViewportInputResult> DragAsync(double fromX, double fromY, double toX, double toY, CancellationToken cancellationToken);
}
