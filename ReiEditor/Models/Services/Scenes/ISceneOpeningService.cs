using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneOpeningService
{
    Task<bool> OpenAsync(string assetId);
}
