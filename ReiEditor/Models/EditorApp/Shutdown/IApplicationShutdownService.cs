using System;
using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Shutdown;

public interface IApplicationShutdownService
{
	void Shutdown(int exitCode);
	void AddShutdownTask(Func<Task> shutdownTask);
}