using System.Collections.Generic;

namespace ReiEditor.Models.Services.Engine.Api.DTO;

public class SetEntityDataRequest
{
    public const string REI_BEHAVIOUR_ID = "REI_BEHAVIOUR_ID";
    
    public int SceneId { get; set; }
    public bool RecordTransformChange { get; set; }
    
    public List<Dictionary<string, object?>> Behaviours { get; set; } = new();
}