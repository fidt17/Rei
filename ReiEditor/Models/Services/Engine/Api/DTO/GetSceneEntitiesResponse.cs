using System.Collections.Generic;

namespace ReiEditor.Models.Services.Engine.Api.DTO;

public class GetSceneEntitiesResponse
{
    public class SceneEntitiesResponseEntity
    {
        public int Id { get; set; }
        public bool IsSelected { get; set; }
    }
    
    public List<SceneEntitiesResponseEntity> Entities { get; set; } = new();
}