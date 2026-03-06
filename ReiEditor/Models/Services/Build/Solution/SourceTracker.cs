using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Build.Solution;

public class SourceTracker : ISourceTracker
{
    private readonly Dictionary<string, int> _sourcePathToHashMap = new();
    private readonly IResourceService _resourceService;

    public SourceTracker(IResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    public async Task<bool> ChangedOrNewSourcesExist()
    {
        var solutionPath = _resourceService.GetScriptsPath();
        
        bool result = false;
        var fileExtensions = new List<string> { FileExtensions.CPP, FileExtensions.H };
        foreach (var extension in fileExtensions)
        {
            foreach (var src in Directory.EnumerateFiles(solutionPath, $"*{extension}", SearchOption.AllDirectories))
            {
                var hash = (await File.ReadAllTextAsync(src)).GetHashCode();
                if (_sourcePathToHashMap.ContainsKey(src))
                {
                    result |= _sourcePathToHashMap[src] != hash;
                }
                else
                {
                    result = true;
                }
                _sourcePathToHashMap[src] = hash;
            }
        }

        return result;
    }
}