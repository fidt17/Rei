using System.Collections.Generic;

namespace ReiEditor.Models.Services.Engine.Api.DTO;

public class SetEntitySelectionRequest
{
    public List<int> EntityIds { get; set; } = new();
}
