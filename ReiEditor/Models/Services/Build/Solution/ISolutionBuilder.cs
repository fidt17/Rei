using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build.Solution;

public interface ISolutionBuilder
{
    Task Build(BuildConfigurationEnum configuration);
}