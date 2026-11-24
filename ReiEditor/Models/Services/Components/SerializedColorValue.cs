using System;
using System.Collections.Generic;

namespace ReiEditor.Models.Services.Components;

public readonly record struct SerializedColorValue(float R, float G, float B, float A)
{
    public static SerializedColorValue? Read(object? value)
    {
        if (value is not Dictionary<string, SerializedProperty> channels
            || !channels.TryGetValue("r", out var r) || !channels.TryGetValue("g", out var g)
            || !channels.TryGetValue("b", out var b) || !channels.TryGetValue("a", out var a)) return null;

        return new(Convert.ToSingle(r.Value), Convert.ToSingle(g.Value), Convert.ToSingle(b.Value), Convert.ToSingle(a.Value));
    }

    public void Apply(SerializedProperty property)
    {
        var channels = (Dictionary<string, SerializedProperty>)property.Value!;
        SetChannel(channels["r"], R);
        SetChannel(channels["g"], G);
        SetChannel(channels["b"], B);
        SetChannel(channels["a"], A);
        property.TriggerChangedEvent();
    }

    private static void SetChannel(SerializedProperty channel, float value)
    {
        if (Convert.ToSingle(channel.Value) != value) channel.SetValueWithoutTriggeringChangedEvent(value);
    }
}
