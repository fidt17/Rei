using Avalonia.Headless.XUnit;
using ReiEditor.Models.ProjectManagement.BookmarkedProjects;
using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.ProjectManagement;
using ReiEditor.ViewModels.Windows.ProjectManagement.Commands;
using ProjectModel = ReiEditor.Models.ProjectManagement.Project;

namespace ReiEditor.Tests.ViewModels.Windows.ProjectManagement;

/// <summary>Verifies project creation state and bookmarked-project list refresh behavior.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "ViewModels")]
public sealed class ProjectCreationAndListViewModelTests : IDisposable
{
    /// <summary>Runs factory callbacks for parameterless or parameterized VM creation.</summary>
    private sealed class TestFactory<T>(Func<object[], T> create) : IFactory<T> where T : class
    {
        public T CreateInstance() => create(Array.Empty<object>());
        public T CreateInstance(params object[] parameters) => create(parameters);
    }

    /// <summary>Supplies controlled project creation and raises normal service outcome events.</summary>
    private sealed class TestProjectCreationService : IProjectCreationService
    {
        public event Action<ProjectModel>? ProjectCreatedEvent;
        public event Action? ProjectCreationFailedEvent;
        public ProjectCreationConfiguration Configuration { get; } = new();
        public ProjectCreationValidator Validator { get; }
        public ProjectModel? Result { get; set; }
        public int CreateCount { get; private set; }

        public TestProjectCreationService()
        {
            Validator = new ProjectCreationValidator(Configuration);
        }

        public Task<ProjectModel?> CreateProject()
        {
            CreateCount++;
            if (Result == null)
            {
                ProjectCreationFailedEvent?.Invoke();
                return Task.FromResult<ProjectModel?>(null);
            }

            ProjectCreatedEvent?.Invoke(Result);
            return Task.FromResult<ProjectModel?>(Result);
        }
    }

    /// <summary>Stores projects and announces each collection mutation.</summary>
    private sealed class TestBookmarkedProjectsService(IEnumerable<ProjectModel>? initial = null) : IBookmarkedProjectsService
    {
        private readonly List<ProjectModel> _projects = initial?.ToList() ?? new List<ProjectModel>();
        public event Action? BookmarkedProjectsCollectionChangedEvent;
        public IEnumerable<ProjectModel> GetBookmarkedProjects() => _projects.ToArray();
        public void AddProject(ProjectModel project)
        {
            _projects.Add(project);
            BookmarkedProjectsCollectionChangedEvent?.Invoke();
        }

        public void RemoveProject(ProjectModel project)
        {
            _projects.Remove(project);
            BookmarkedProjectsCollectionChangedEvent?.Invoke();
        }
    }

    /// <summary>Tracks disposal when project list rebuilds its item VMs.</summary>
    private sealed class TestProjectsListElementViewModel : ProjectsListElementViewModel
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }

    private readonly TemporaryDirectory _directory = new();

    /// <summary>Name edits flow into configuration and refresh projected path and validation notifications.</summary>
    [AvaloniaFact]
    public void TestProjectNameEditUpdatesConfigurationPathAndNotifications()
    {
        var creation = new TestProjectCreationService();
        creation.Configuration.ParentDirectoryPath = _directory.RootPath;
        using var vm = new ProjectCreationTabViewModel(null!, creation, new TestBookmarkedProjectsService());

        vm.ProjectName = "Game";

        Assert.Equal("Game", creation.Configuration.ProjectName);
        Assert.Equal(Path.Combine(_directory.RootPath, "Game"), vm.ProjectPath);
        Assert.True(vm.Notifications.ProjectNameValid);
        Assert.True(vm.Notifications.ProjectPathValid);
        Assert.True(vm.CreateProjectCommand.CanExecute(null));
    }

    /// <summary>Successful project creation bookmarks project and clears failure notification.</summary>
    [AvaloniaFact]
    public async Task TestSuccessfulProjectCreationBookmarksResult()
    {
        var result = TestCreateProject("Created");
        var creation = new TestProjectCreationService { Result = result };
        creation.Configuration.ParentDirectoryPath = _directory.RootPath;
        var bookmarks = new TestBookmarkedProjectsService();
        using var vm = new ProjectCreationTabViewModel(null!, creation, bookmarks);
        vm.ProjectName = "Created";
        var executed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.CreateProjectCommand.ExecutedCommandEvent += resultValue => executed.TrySetResult(resultValue);

        vm.CreateProjectCommand.Execute(null);
        Assert.True(await executed.Task.WaitAsync(TimeSpan.FromSeconds(3)));

        Assert.Same(result, Assert.Single(bookmarks.GetBookmarkedProjects()));
        Assert.False(vm.Notifications.ProjectCreationFailed);
        Assert.Equal(1, creation.CreateCount);
    }

    /// <summary>Failed project creation leaves configuration intact and exposes failure notification.</summary>
    [AvaloniaFact]
    public async Task TestFailedProjectCreationPreservesInputAndReportsFailure()
    {
        var creation = new TestProjectCreationService();
        creation.Configuration.ParentDirectoryPath = _directory.RootPath;
        using var vm = new ProjectCreationTabViewModel(null!, creation, new TestBookmarkedProjectsService());
        vm.ProjectName = "RetryMe";
        var executed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.CreateProjectCommand.ExecutedCommandEvent += resultValue => executed.TrySetResult(resultValue);

        vm.CreateProjectCommand.Execute(null);
        Assert.False(await executed.Task.WaitAsync(TimeSpan.FromSeconds(3)));

        Assert.Equal("RetryMe", vm.ProjectName);
        Assert.Equal(Path.Combine(_directory.RootPath, "RetryMe"), vm.ProjectPath);
        Assert.True(vm.CreateProjectCommand.CanExecute(null));
    }

    /// <summary>Bookmark mutation rebuilds list and disposes replaced item VMs.</summary>
    [AvaloniaFact]
    public void TestBookmarkedProjectChangeRebuildsAndDisposesOldItems()
    {
        var bookmarks = new TestBookmarkedProjectsService(new[] { TestCreateProject("One") });
        var createdElements = new List<TestProjectsListElementViewModel>();
        var elementFactory = new TestFactory<ProjectsListElementViewModel>(_ =>
        {
            var element = new TestProjectsListElementViewModel();
            createdElements.Add(element);
            return element;
        });
        var openFactory = new TestFactory<OpenProjectCommand>(_ => new OpenProjectCommand(null!, null!, null!, bookmarks));
        using var vm = new ProjectsListTabViewModel(bookmarks, elementFactory, openFactory);
        var firstElement = createdElements.Single();

        bookmarks.AddProject(TestCreateProject("Two"));

        Assert.Equal(2, vm.AvailableProjects.Count);
        Assert.Equal(1, firstElement.DisposeCount);
        Assert.Equal(3, createdElements.Count);
    }

    /// <summary>Creates project metadata rooted inside isolated test directory.</summary>
    private ProjectModel TestCreateProject(string name)
    {
        var project = new ProjectModel();
        project.SetProjectName(name);
        project.SetProjectFilePath(_directory.GetPath($"{name}.reiproj"));
        return project;
    }

    /// <summary>Deletes isolated project paths.</summary>
    public void Dispose() => _directory.Dispose();
}
