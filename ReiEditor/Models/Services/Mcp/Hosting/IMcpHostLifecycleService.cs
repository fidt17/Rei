using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Mcp.Hosting;

public interface IMcpHostLifecycleService
{
    Task StartAsync();
}
