namespace ReiEditor.Mcp.Contracts;

public interface IReiEditorGateway
{
    Task<ReiEditorState> GetStateAsync(CancellationToken cancellationToken);
    Task<ReiEntityList> ListEntitiesAsync(CancellationToken cancellationToken);
    Task<ReiEntityDetails> GetEntityAsync(int entityId, CancellationToken cancellationToken);
    Task<ReiEntityMutationResult> RenameEntityAsync(int entityId, string newName, CancellationToken cancellationToken);
    Task<ReiProjectSaveResult> SaveProjectAsync(CancellationToken cancellationToken);
}
