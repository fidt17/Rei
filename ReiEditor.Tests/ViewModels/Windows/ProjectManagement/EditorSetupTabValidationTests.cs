using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.ViewModels.Windows.ProjectManagement;

namespace ReiEditor.Tests.ViewModels.Windows.ProjectManagement;

/// <summary>Verifies editor setup validation snapshots and configuration-change lifecycle.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "ViewModels")]
public sealed class EditorSetupTabValidationTests
{
    /// <summary>Configures mutable editor validation values and raises configuration events.</summary>
    private sealed class TestEditorSettingsService : IEditorSettingsService
    {
        public event Action<bool>? EditorConfigurationChangedEvent;
        public event Action? ConfigurationSetEvent;

        public bool ConfigurationValid { get; set; }
        public bool EngineValid { get; set; }
        public bool MsBuildValid { get; set; }

        public Task InitializeAsync() => Task.CompletedTask;
        public bool IsEditorConfigurationValid() => ConfigurationValid;
        public void SaveConfiguration() => ConfigurationSetEvent?.Invoke();
        public bool IsEngineLocationValid() => EngineValid;
        public bool SetEngineLocation(string path) => true;
        public string GetEngineLocation() => string.Empty;
        public bool IsMsBuildLocationValid() => MsBuildValid;
        public bool SetMsBuildLocation(string path) => true;
        public string GetMsBuildLocation() => string.Empty;
        public bool IsTextEditorLocationValid() => true;
        public bool SetTextEditorLocation(string path) => true;
        public void ClearTextEditorLocation() { }
        public string GetTextEditorLocation() => string.Empty;
        public void RaiseConfigurationChanged() => EditorConfigurationChangedEvent?.Invoke(ConfigurationValid);
    }

    /// <summary>Initial state reflects each underlying validation flag.</summary>
    [Fact]
    public void TestConstructorReadsCurrentValidationState()
    {
        var settings = new TestEditorSettingsService
        {
            ConfigurationValid = false,
            EngineValid = true,
            MsBuildValid = false,
        };

        using var validation = new EditorSetupTabValidation(settings);

        Assert.False(validation.IsEditorConfigurationValid);
        Assert.True(validation.IsEnginePathValid);
        Assert.False(validation.IsMsBuildPathValid);
    }

    /// <summary>Configuration event re-reads path checks instead of trusting only event payload.</summary>
    [Fact]
    public void TestConfigurationChangeRefreshesAllValidationState()
    {
        var settings = new TestEditorSettingsService();
        using var validation = new EditorSetupTabValidation(settings);
        settings.ConfigurationValid = true;
        settings.EngineValid = true;
        settings.MsBuildValid = true;

        settings.RaiseConfigurationChanged();

        Assert.True(validation.IsEditorConfigurationValid);
        Assert.True(validation.IsEnginePathValid);
        Assert.True(validation.IsMsBuildPathValid);
    }

    /// <summary>Disposed validation stops observing later settings changes.</summary>
    [Fact]
    public void TestDisposeStopsConfigurationUpdates()
    {
        var settings = new TestEditorSettingsService();
        var validation = new EditorSetupTabValidation(settings);
        validation.Dispose();
        settings.ConfigurationValid = true;
        settings.EngineValid = true;
        settings.MsBuildValid = true;

        settings.RaiseConfigurationChanged();

        Assert.False(validation.IsEditorConfigurationValid);
        Assert.False(validation.IsEnginePathValid);
        Assert.False(validation.IsMsBuildPathValid);
    }
}
