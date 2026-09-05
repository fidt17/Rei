using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.EditorApp.ViewportGrid;

/// <summary>Verifies viewport-grid exclusivity, engine updates, and preference persistence.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Viewport")]
public sealed class ViewportGridServiceTests
{
    /// <summary>Records value snapshots sent to engine.</summary>
    private sealed class TestApi : TestEngineApi
    {
        public List<SetViewportGridSettingsRequest> Settings { get; } = new();
        public override void SetEditorGridSettings(SetViewportGridSettingsRequest settings) => Settings.Add(settings);
    }

    /// <summary>Stores one configured grid settings instance and records persistence.</summary>
    private sealed class TestPreferences(ViewportGridSettings settings) : IEditorPreferencesService
    {
        public ViewportGridSettings Settings { get; } = settings;
        public ViewportGridSettings? SavedSettings { get; private set; }
        public Task InitializeAsync() => Task.CompletedTask;
        public ViewportGridSettings GetGridSettings() => Settings;
        public void SetGridSettings(ViewportGridSettings value) => SavedSettings = value;
        public string? GetEnginePath() => throw new NotSupportedException();
        public void SetEnginePath(string path) => throw new NotSupportedException();
        public string? GetMsBuildPath() => throw new NotSupportedException();
        public void SetMsBuildPath(string path) => throw new NotSupportedException();
        public string? GetTextEditorPath() => throw new NotSupportedException();
        public void SetTextEditorPath(string path) => throw new NotSupportedException();
        public IEnumerable<ReiEditor.Models.ProjectManagement.Project> GetBookmarkedProjects() => throw new NotSupportedException();
        public void SetBookmarkedProjects(IEnumerable<ReiEditor.Models.ProjectManagement.Project> paths) => throw new NotSupportedException();
        public ConsolePreferences GetConsolePreferences() => throw new NotSupportedException();
        public void SetConsolePreferences(ConsolePreferences consolePreferences) => throw new NotSupportedException();
        public string GetWindowContainerActiveTab(string tag) => throw new NotSupportedException();
        public void SetWindowContainerActiveTab(string tag, string tabName) => throw new NotSupportedException();
    }

    /// <summary>Enabling each grid plane disables both other planes and sends one engine snapshot.</summary>
    [Theory]
    [InlineData("XY")]
    [InlineData("YZ")]
    [InlineData("XZ")]
    public void TestEnablingPlaneIsMutuallyExclusive(string plane)
    {
        var settings = plane switch
        {
            "XZ" => new ViewportGridSettings { RenderXZ = false, RenderXY = true, RenderYZ = false },
            "XY" => new ViewportGridSettings { RenderXZ = false, RenderXY = false, RenderYZ = true },
            _ => new ViewportGridSettings { RenderXZ = true, RenderXY = false, RenderYZ = false },
        };
        var preferences = new TestPreferences(settings);
        var api = new TestApi();
        using var service = new ViewportGridService(preferences, api, new TestEngineRunner());

        if (plane == "XZ") service.EnableXZGrid(true);
        else if (plane == "XY") service.EnableXYGrid(true);
        else service.EnableYZGrid(true);

        var sent = api.Settings[^1];
        Assert.Equal(plane == "XZ", sent.RenderXZ);
        Assert.Equal(plane == "XY", sent.RenderXY);
        Assert.Equal(plane == "YZ", sent.RenderYZ);
        Assert.Single(api.Settings);
    }

    /// <summary>Setting a plane to its current value performs no engine call.</summary>
    [Fact]
    public void TestUnchangedPlaneValueDoesNotSendSettings()
    {
        var settings = new ViewportGridSettings { RenderXZ = true };
        var api = new TestApi();
        using var service = new ViewportGridService(new TestPreferences(settings), api, new TestEngineRunner());

        service.EnableXZGrid(true);

        Assert.Empty(api.Settings);
    }

    /// <summary>Opacity values are retained as supplied and sent to engine.</summary>
    [Theory]
    [InlineData(-1.0f)]
    [InlineData(0.75f)]
    [InlineData(2.0f)]
    public void TestOpacityIsForwardedWithoutClamping(float opacity)
    {
        var settings = new ViewportGridSettings();
        var api = new TestApi();
        using var service = new ViewportGridService(new TestPreferences(settings), api, new TestEngineRunner());

        service.SetOpacity(opacity);

        Assert.Equal(opacity, settings.Opacity);
        Assert.Equal(opacity, Assert.Single(api.Settings).Opacity);
    }

    /// <summary>Dispose saves current settings and detaches engine-start handling.</summary>
    [Fact]
    public void TestDisposePersistsSettingsAndDetachesEngineStartedEvent()
    {
        var settings = new ViewportGridSettings();
        var preferences = new TestPreferences(settings);
        var api = new TestApi();
        var runner = new TestEngineRunner();
        var service = new ViewportGridService(preferences, api, runner);
        service.SetOpacity(0.6f);
        Assert.Equal(1, runner.EngineStartedSubscriberCount);

        service.Dispose();

        Assert.Equal(0, runner.EngineStartedSubscriberCount);
        Assert.Same(settings, preferences.SavedSettings);
        Assert.Single(api.Settings);
    }
}
