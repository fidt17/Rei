using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
	public Project Project { get; }

#pragma warning disable CS8618
	public ProjectEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ProjectEditorWindowViewModel(IActiveProjectService activeProjectService)
	{
		Project = activeProjectService.GetActiveProject();

		try
		{
			ProjectApi.LoadDll(@"C:\Repos\Rei Projects\First Project\Solution\Project.dll");
			Task.Run(() =>
			{
				ProjectApi.Start();
			});
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}
}

public static class ProjectApi
{
	private const string PROJECT_DLL = "Project.dll";
	
	public static void LoadDll(string path)
	{
		try
		{
			NativeLibrary.Load(path);
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}

	[DllImport(PROJECT_DLL)]
	public static extern void Start();
}