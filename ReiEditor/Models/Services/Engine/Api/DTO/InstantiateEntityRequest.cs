namespace ReiEditor.Models.Services.Engine.Api.DTO;

public class InstantiateEntityRequest
{
    public int SourceEntityId { get; set; }
    public string RequestedName { get; set; } = "";
    public bool IncludeChildren { get; set; } = true;
}
