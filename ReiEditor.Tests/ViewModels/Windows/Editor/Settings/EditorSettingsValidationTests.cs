using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.ViewModels.Windows.Editor.Settings;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Settings;

/// <summary>Verifies settings-window validation refresh and disposal behavior.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "ViewModels")]
public sealed class EditorSettingsValidationTests
{
    /// <summary>Supplies mutable path checks to settings validation.</summary>
    private sealed class TestEditorSettingsService : IEditorSettingsService
    {
        public event Action<bool>? EditorConfigurationChangedEvent;
        public event Action? ConfigurationSetEvent;
        public bool MsBuildValid { get; set; }
        public bool TextEditorValid { get; set; }
        public Task InitializeAsync() => Task.CompletedTask;
        public bool IsEditorConfigurationValid() => MsBuildValid;
        public void SaveConfiguration() => ConfigurationSetEvent?.Invoke();
        public bool IsEngineLocationValid() => true;
        public bool SetEngineLocation(string path) => true;
        public string GetEngineLocation() => string.Empty;
        public bool IsMsBuildLocationValid() => MsBuildValid;
        public bool SetMsBuildLocation(string path) => true;
        public string GetMsBuildLocation() => string.Empty;
        public bool IsTextEditorLocationValid() => TextEditorValid;
        public bool SetTextEditorLocation(string path) => true;
        public void ClearTextEditorLocation() { }
        public string GetTextEditorLocation() => string.Empty;
        public void RaiseConfigurationChanged() => EditorConfigurationChangedEvent?.Invoke(false);
    }

    /// <summary>Construction and configuration events expose current independent path validity.</summary>
    [Fact]
    public void TestConfigurationChangeRefreshesPathValidity()
    {
        var settings = new TestEditorSettingsService { MsBuildValid = true };
        using var validation = new EditorSettingsValidation(settings);
        Assert.True(validation.IsMsBuildPathValid);
        Assert.False(validation.IsTextEditorPathValid);
        settings.MsBuildValid = false;
        settings.TextEditorValid = true;

        settings.RaiseConfigurationChanged();

        Assert.False(validation.IsMsBuildPathValid);
        Assert.True(validation.IsTextEditorPathValid);
    }

    /// <summary>Disposed validation preserves its last state when settings later change.</summary>
    [Fact]
    public void TestDisposeStopsPathValidityUpdates()
    {
        var settings = new TestEditorSettingsService();
        var validation = new EditorSettingsValidation(settings);
        validation.Dispose();
        settings.MsBuildValid = true;
        settings.TextEditorValid = true;

        settings.RaiseConfigurationChanged();

        Assert.False(validation.IsMsBuildPathValid);
        Assert.False(validation.IsTextEditorPathValid);
    }
}
