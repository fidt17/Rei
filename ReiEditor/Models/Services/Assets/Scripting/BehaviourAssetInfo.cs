using System.Collections.Generic;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;

namespace ReiEditor.Models.Services.Assets.Scripting;

public class BehaviourAssetInfo : SerializableObjectInfo
{
    public int BehaviourId { get; }
    public IReadOnlyList<string> RequiredComponentNames { get; }

    public BehaviourAssetInfo(string behaviourNamespace,
        string objectName,
        int behaviourId,
        ObjectFile<string> source,
        Dictionary<string, SerializedPropertyData> serializedProperties,
        IReadOnlyList<string> requiredComponentNames,
        string includePath)
        : base(behaviourNamespace, objectName, false, source, serializedProperties, includePath)
    {
        BehaviourId = behaviourId;
        RequiredComponentNames = requiredComponentNames;
    }
}
