using System.Threading;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build;

public interface IBuildPreparationService
{
    Task Prepare(CancellationToken cancellationToken);
}
