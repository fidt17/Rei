using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build.ProjectBuild;

public class ProjectBuildConfigurationUtility
{
    private const string GENERATED_BUILD_DIRECTORY = "Build\\Generated";
    private const string GENERATED_RC_FILE_NAME = "AppIcon.rc";
    private const string GENERATED_ICON_FILE_NAME = "AppIcon.ico";

    private readonly IActiveProjectService _activeProjectService;
    private readonly ILogger<ProjectBuildConfigurationUtility> _logger;

    public ProjectBuildConfigurationUtility(
        IActiveProjectService activeProjectService,
        ILogger<ProjectBuildConfigurationUtility> logger)
    {
        _activeProjectService = activeProjectService;
        _logger = logger;
    }

    public void ApplyExecutableBuildSettings(ProjectBuildRequest request)
    {
        var project = _activeProjectService.GetActiveProject();
        var projectFilePath = project.ProjectVisualStudioProjectPath;

        if (!File.Exists(projectFilePath))
        {
            throw new Exception($"Missing project file: {projectFilePath}");
        }

        var doc = XDocument.Load(projectFilePath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var configurationName = request.Configuration == BuildConfigurationEnum.Release ? "Release" : "Debug";

        foreach (var itemDefinitionGroup in doc.Descendants(ns + "ItemDefinitionGroup"))
        {
            var condition = itemDefinitionGroup.Attribute("Condition")?.Value;
            if (!MatchesConfigurationCondition(condition, configurationName)) continue;

            var link = itemDefinitionGroup.Element(ns + "Link");
            if (link == null)
            {
                link = new XElement(ns + "Link");
                itemDefinitionGroup.Add(link);
            }

            var subSystem = link.Element(ns + "SubSystem");
            if (subSystem == null)
            {
                subSystem = new XElement(ns + "SubSystem");
                link.Add(subSystem);
            }

            subSystem.Value = request.ShowConsole ? "Console" : "Windows";

            var entryPointSymbol = link.Element(ns + "EntryPointSymbol");
            if (request.ShowConsole)
            {
                entryPointSymbol?.Remove();
                continue;
            }

            if (entryPointSymbol == null)
            {
                entryPointSymbol = new XElement(ns + "EntryPointSymbol");
                link.Add(entryPointSymbol);
            }

            entryPointSymbol.Value = "mainCRTStartup";
        }

        var scriptsDirectoryPath = Path.GetDirectoryName(projectFilePath);
        if (scriptsDirectoryPath == null)
        {
            throw new Exception("Could not resolve scripts directory for project file.");
        }

        ApplyIcon(doc, ns, scriptsDirectoryPath, request.IconPath);
        doc.Save(projectFilePath);
    }

    private void ApplyIcon(XDocument doc, XNamespace ns, string scriptsDirectoryPath, string iconPath)
    {
        var normalizedIconPath = iconPath?.Trim() ?? string.Empty;
        var relativeRcPath = $"{GENERATED_BUILD_DIRECTORY}\\{GENERATED_RC_FILE_NAME}";

        if (string.IsNullOrWhiteSpace(normalizedIconPath))
        {
            RemoveGeneratedRcInclude(doc, ns, relativeRcPath);
            CleanupGeneratedIconFiles(scriptsDirectoryPath);
            return;
        }

        var fullIconPath = Path.GetFullPath(normalizedIconPath);
        if (!File.Exists(fullIconPath))
        {
            throw new Exception($"Icon file does not exist: {fullIconPath}");
        }

        var generatedDirectoryPath = Path.Combine(scriptsDirectoryPath, GENERATED_BUILD_DIRECTORY);
        Directory.CreateDirectory(generatedDirectoryPath);

        var generatedIconPath = Path.Combine(generatedDirectoryPath, GENERATED_ICON_FILE_NAME);
        var generatedRcPath = Path.Combine(generatedDirectoryPath, GENERATED_RC_FILE_NAME);

        File.Copy(fullIconPath, generatedIconPath, overwrite: true);
        File.WriteAllText(generatedRcPath, "IDI_APP_ICON ICON \"AppIcon.ico\"\r\n");

        var rcInclude = doc
            .Descendants(ns + "ResourceCompile")
            .FirstOrDefault(x => string.Equals(
                NormalizeIncludePath(x.Attribute("Include")?.Value),
                NormalizeIncludePath(relativeRcPath),
                StringComparison.OrdinalIgnoreCase));

        if (rcInclude != null) return;

        var itemGroup = doc.Descendants(ns + "ItemGroup")
            .FirstOrDefault(x => x.Elements(ns + "ResourceCompile").Any());

        if (itemGroup == null)
        {
            itemGroup = new XElement(ns + "ItemGroup");
            var importCppTargets = doc.Descendants(ns + "Import")
                .FirstOrDefault(x => string.Equals(
                    x.Attribute("Project")?.Value,
                    "$(VCTargetsPath)\\Microsoft.Cpp.targets",
                    StringComparison.OrdinalIgnoreCase));

            if (importCppTargets != null)
            {
                importCppTargets.AddBeforeSelf(itemGroup);
            }
            else
            {
                doc.Root?.Add(itemGroup);
            }
        }

        itemGroup.Add(new XElement(ns + "ResourceCompile", new XAttribute("Include", relativeRcPath)));
    }

    private static bool MatchesConfigurationCondition(string? condition, string configurationName)
    {
        if (string.IsNullOrWhiteSpace(condition)) return false;
        return condition.Contains($"'{configurationName}|x64'", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIncludePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        return path.Replace('/', '\\').Trim();
    }

    private void RemoveGeneratedRcInclude(XDocument doc, XNamespace ns, string relativeRcPath)
    {
        var existingRcIncludes = doc.Descendants(ns + "ResourceCompile")
            .Where(x => string.Equals(
                NormalizeIncludePath(x.Attribute("Include")?.Value),
                NormalizeIncludePath(relativeRcPath),
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var existingRc in existingRcIncludes)
        {
            var parent = existingRc.Parent;
            existingRc.Remove();
            if (parent is { Name.LocalName: "ItemGroup" } && !parent.Elements().Any())
            {
                parent.Remove();
            }
        }
    }

    private void CleanupGeneratedIconFiles(string scriptsDirectoryPath)
    {
        try
        {
            var generatedDirectoryPath = Path.Combine(scriptsDirectoryPath, GENERATED_BUILD_DIRECTORY);
            var generatedIconPath = Path.Combine(generatedDirectoryPath, GENERATED_ICON_FILE_NAME);
            var generatedRcPath = Path.Combine(generatedDirectoryPath, GENERATED_RC_FILE_NAME);

            if (File.Exists(generatedIconPath)) File.Delete(generatedIconPath);
            if (File.Exists(generatedRcPath)) File.Delete(generatedRcPath);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }
}
