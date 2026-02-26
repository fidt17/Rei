using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Creation.Behaviour;

public interface IBehaviourCreationUtility
{
    Task<bool> CreateBehaviourAsync(BehaviourCreationSettings settings);
}
