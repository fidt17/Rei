using System.Collections.Generic;

namespace ReiEditor.Models.Services.Engine.Api.DTO;

public class GetSceneEntitiesResponse
{
    public List<int> Entities { get; set; } = new();
}