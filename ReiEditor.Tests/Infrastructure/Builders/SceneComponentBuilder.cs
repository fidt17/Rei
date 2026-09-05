using System.Numerics;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.RectTransform;

namespace ReiEditor.Tests.Infrastructure.Builders;

/// <summary>Builds hydrated scene component properties using explicit test values.</summary>
internal static class SceneComponentBuilder
{
    public static SerializedProperty Vector(string name, params (string Name, float Value)[] values)
    {
        var property = new SerializedProperty(name, SerializedTypeEnum.Custom, null, "Vector", null);
        property.Value = values.ToDictionary(x => x.Name, x => new SerializedProperty(x.Name, SerializedTypeEnum.Float, x.Value, "float", property));
        return property;
    }

    public static BehaviourComponent Transform(int id, Vector3 position, Vector3 rotation)
    {
        var component = new BehaviourComponent(id);
        component.AddProperty(Vector(EngineBehavioursConstants.TRANSFORM_POSITION, ("x", position.X), ("y", position.Y), ("z", position.Z)));
        component.AddProperty(Vector(EngineBehavioursConstants.TRANSFORM_ROTATION, ("x", rotation.X), ("y", rotation.Y), ("z", rotation.Z)));
        return component;
    }

    public static BehaviourComponent RectTransform(int id, RectTransformLayoutData layout)
    {
        var component = new BehaviourComponent(id);
        component.AddProperty(Vector(EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN, ("x", layout.AnchorMin.X), ("y", layout.AnchorMin.Y)));
        component.AddProperty(Vector(EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX, ("x", layout.AnchorMax.X), ("y", layout.AnchorMax.Y)));
        component.AddProperty(Vector(EngineBehavioursConstants.RECT_TRANSFORM_PIVOT, ("x", layout.Pivot.X), ("y", layout.Pivot.Y)));
        component.AddProperty(Vector(EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, ("x", layout.AnchoredPosition.X), ("y", layout.AnchoredPosition.Y)));
        component.AddProperty(Vector(EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, ("x", layout.SizeDelta.X), ("y", layout.SizeDelta.Y)));
        return component;
    }
}
