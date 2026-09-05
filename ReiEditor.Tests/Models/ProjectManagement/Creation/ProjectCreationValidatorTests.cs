using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.ProjectManagement.Creation;

/// <summary>Verifies project name, destination path, and combined configuration validation.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "ProjectManagement")]
public sealed class ProjectCreationValidatorTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    /// <summary>Accepts longest supported project name when destination does not exist.</summary>
    [Fact]
    public void TestAcceptsThirtyOneCharacterNameAndNewDestination()
    {
        var configuration = CreateConfiguration(new string('a', 31));
        var validator = new ProjectCreationValidator(configuration);

        Assert.True(validator.IsProjectNameValid());
        Assert.True(validator.IsProjectPathValid());
        Assert.True(validator.IsConfigurationValid());
    }

    /// <summary>Rejects blank, overlong, and invalid filename project names.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Project<Name")]
    public void TestRejectsInvalidProjectNames(string projectName)
    {
        var validator = new ProjectCreationValidator(CreateConfiguration(projectName));

        Assert.False(validator.IsProjectNameValid());
        Assert.False(validator.IsConfigurationValid());
    }

    /// <summary>Rejects existing destination regardless of whether directory is empty.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TestRejectsExistingDestination(bool containsFile)
    {
        var configuration = CreateConfiguration("Existing");
        Directory.CreateDirectory(configuration.FullPath);
        if (containsFile) File.WriteAllText(Path.Combine(configuration.FullPath, "sentinel.txt"), "keep");
        var validator = new ProjectCreationValidator(configuration);

        Assert.False(validator.IsProjectPathValid());
        Assert.False(validator.IsConfigurationValid());
    }

    /// <summary>Configuration updates keep full path synchronized and same-value assignments emit no event.</summary>
    [Fact]
    public void TestConfigurationUpdatesFullPath()
    {
        var configuration = CreateConfiguration("First");
        var changedCount = 0;
        configuration.ConfigurationChangedEvent += () => changedCount++;

        configuration.ProjectName = "Second";
        var secondParent = _directory.GetPath("Nested");
        configuration.ParentDirectoryPath = secondParent;

        Assert.Equal(Path.Combine(secondParent, "Second"), configuration.FullPath);

        changedCount = 0;
        configuration.ParentDirectoryPath = secondParent;
        configuration.ProjectName = "Second";
        Assert.Equal(0, changedCount);
    }

    /// <summary>Creates configuration rooted in isolated test directory.</summary>
    private ProjectCreationConfiguration CreateConfiguration(string projectName)
    {
        return new ProjectCreationConfiguration
        {
            ParentDirectoryPath = _directory.RootPath,
            ProjectName = projectName
        };
    }

    /// <summary>Deletes isolated project destinations.</summary>
    public void Dispose() => _directory.Dispose();
}
