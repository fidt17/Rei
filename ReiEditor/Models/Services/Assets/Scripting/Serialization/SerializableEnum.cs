using System.Collections.Generic;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public class SerializableEnum
{
    public string Namespace { get; set; } = "";
    public string EnumName { get; set; } = "";
    public string IncludePath { get; set; } = "";

    public Dictionary<string, int> Options { get; set; } = new();
}