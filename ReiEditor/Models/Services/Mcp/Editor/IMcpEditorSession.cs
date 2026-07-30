using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal interface IMcpEditorSession
{
    ReiEditorState GetState();
    ReiEntityList ListEntities();
    ReiEntityDetails GetEntity(int entityId);
    ReiEntityMutationResult RenameEntity(int entityId, string newName);
    Task<ReiProjectSaveResult> SaveProjectAsync();
}
