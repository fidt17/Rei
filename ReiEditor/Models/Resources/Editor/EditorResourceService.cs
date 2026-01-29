using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Resources.Editor;

public class EditorResourceService : IEditorResourceService
{
    private readonly ILogger<EditorResourceService> _logger;
    private readonly string _resourcesPath;

    public EditorResourceService(ILogger<EditorResourceService> logger)
    {
        _logger = logger;
        _resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ResourceConstants.RESOURCES_DIR_NAME);
    }

    public async Task<string?> Load(params string[] path)
    {
        try
        {
            return await ResourceUtils.Load(Path.Combine(_resourcesPath, Path.Combine(path)));
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }

        return null;
    }
}
