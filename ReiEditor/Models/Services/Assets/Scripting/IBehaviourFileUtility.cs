using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Meta;

namespace ReiEditor.Models.Services.Assets.Scripting;

public interface IBehaviourFileUtility
{
    List<BehaviourFileUtility.BehaviourPathData> GetAllBehaviours();
    Task<List<ObjectFile<AssetMeta>>> GetAllBehaviourMetas();
    bool TryGetBehaviourNameFrom(string text, out string name);
    Task<bool> IsBehaviourFile(string path);
}
