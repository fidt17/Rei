using System.Threading.Tasks;

namespace ReiEditor.Models.Resources.EngineResources;

public interface IEngineResourcesImporter
{
    Task Import();
}