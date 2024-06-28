using System;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Build;

namespace ReiEditor.Models.EditorApp.Refresh;

public class RefreshProjectOnWindowActivationSystem : IDisposable
{
    private readonly IMainWindowService _mainWindowService;
    private readonly IBuildStarter _buildStarter;

    public RefreshProjectOnWindowActivationSystem(IMainWindowService mainWindowService, IBuildStarter buildStarter)
    {
        _mainWindowService = mainWindowService;
        _buildStarter = buildStarter;
        
        _mainWindowService.ActivatedEvent += HandleMainWindowActivatedEvent;
    }

    public void Dispose()
    {
        _mainWindowService.ActivatedEvent -= HandleMainWindowActivatedEvent;
    }

    private void HandleMainWindowActivatedEvent()
    {
        if (!_buildStarter.CanStartBuild.IsTrue.Value) return;
        
        // _buildStarter.BuildProject(BuildConfigurationEnum.EditorDebug);
    }
}