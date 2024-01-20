using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public interface IBehaviourComponentsService
{
    Task<int> ImportBehaviours();
}