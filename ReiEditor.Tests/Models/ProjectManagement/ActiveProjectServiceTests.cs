using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.ProjectManagement;

/// <summary>
/// Verifies active project state and notifications.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "ProjectManagement")]
public sealed class ActiveProjectServiceTests
{
    /// <summary>
    /// Reading active project before one is opened fails explicitly.
    /// </summary>
    [Fact]
    public void GetActiveProjectBeforeOpenThrows()
    {
        var service = new ActiveProjectService(new TestLogger<ActiveProjectService>());

        Assert.Throws<NullReferenceException>(() => service.GetActiveProject());
    }

    /// <summary>
    /// Opening a project stores that exact instance and publishes it.
    /// </summary>
    [Fact]
    public void OpenProjectStoresAndPublishesSelectedInstance()
    {
        var service = new ActiveProjectService(new TestLogger<ActiveProjectService>());
        var first = new Project();
        var second = new Project();
        var published = new List<Project>();
        service.ActiveProjectChangedEvent += published.Add;

        service.OpenProject(first);
        service.OpenProject(second);

        Assert.Same(second, service.GetActiveProject());
        Assert.Equal(new[] { first, second }, published);
    }

    /// <summary>
    /// Reopening same instance still publishes active project change.
    /// </summary>
    [Fact]
    public void ReopeningSameInstancePublishesAgain()
    {
        var service = new ActiveProjectService(new TestLogger<ActiveProjectService>());
        var project = new Project();
        var notifications = 0;
        service.ActiveProjectChangedEvent += _ => notifications++;

        service.OpenProject(project);
        service.OpenProject(project);

        Assert.Equal(2, notifications);
    }
}
