using System;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Scenes;

public interface IEngineSceneLoader
{
    Task<bool> LoadSceneAsync(Scene? scene, Action<int>? onProgress = null);
}
