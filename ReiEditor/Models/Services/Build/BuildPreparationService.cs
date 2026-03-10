using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Build;

public class BuildPreparationService : IBuildPreparationService
{
    private readonly IAssetsService _assetsService;

    public BuildPreparationService(IAssetsService assetsService)
    {
        _assetsService = assetsService;
    }

    public async Task Prepare(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _assetsService.SaveProject();
        cancellationToken.ThrowIfCancellationRequested();
    }
}
