using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Models.Services.Scenes;

internal static class SceneTransformDataApplier
{
    public static void Apply(Scene scene, IBehaviourRegistry behaviourRegistry)
    {
        var transformBehaviourId = behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM);
        if (transformBehaviourId == null) return;

        foreach (var entity in scene.Entities)
        {
            var behaviour = entity.GetBehaviour(transformBehaviourId.Value);
            if (behaviour == null) continue;

            if (TryGetIntProperty(behaviour, EngineBehavioursConstants.TRANSFORM_PARENT, out var parent))
            {
                entity.Transform.SetParent(parent);
            }

            if (TryGetIntProperty(behaviour, EngineBehavioursConstants.TRANSFORM_ORDER, out var order))
            {
                entity.Transform.SetOrder(order);
            }
        }
    }

    private static bool TryGetIntProperty(BehaviourComponent component, string name, out int value)
    {
        value = 0;
        if (!component.HasProperty(name)) return false;

        var propertyValue = component.GetProperty(name).Value;
        switch (propertyValue)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = (int)longValue;
                return true;
            case JToken token:
                value = token.ToObject<int>();
                return true;
            default:
                return false;
        }
    }
}
