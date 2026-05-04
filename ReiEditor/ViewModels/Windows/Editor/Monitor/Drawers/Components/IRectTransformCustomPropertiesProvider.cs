using System.Collections.Generic;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

public interface IRectTransformCustomPropertiesProvider
{
    IEnumerable<BaseViewModel> CreateProperties(GameEntity entity, BehaviourComponent component);
}
