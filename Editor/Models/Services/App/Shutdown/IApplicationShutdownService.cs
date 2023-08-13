using System;
using System.Threading.Tasks;

namespace Editor.Models.Services.App.Shutdown;

public interface IApplicationShutdownService
{
	void Shutdown(int exitCode);
	void AddShutdownTask(Func<Task> shutdownTask);
}