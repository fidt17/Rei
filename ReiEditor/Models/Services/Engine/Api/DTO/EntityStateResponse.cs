using System.Collections.Generic;

namespace ReiEditor.Models.Services.Engine.Api.DTO;

public class EntityStateResponse
{
    public int SceneId { get; set; }
    
    public int EntityId { get; set; }
    public int EntityGeneration { get; set; }
    
    public string Name { get; set; } = "";
    public List<Dictionary<string, object>> Behaviours { get; set; } = new();
}