using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build.Solution;

public interface ISourceTracker
{
    Task<bool> ChangedOrNewSourcesExist();
}