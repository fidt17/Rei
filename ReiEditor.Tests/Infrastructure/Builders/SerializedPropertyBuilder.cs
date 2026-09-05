using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Tests.Infrastructure.Builders;

public static class SerializedPropertyBuilder
{
    public static SerializedProperty Integer(string name = "count", int value = 0)
        => new(name, SerializedTypeEnum.Integer, value, "int", parentProperty: null);
}
