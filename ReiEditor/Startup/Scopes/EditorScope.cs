using System.Threading.Tasks;
using Autofac;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor;

namespace ReiEditor.Startup.Scopes;

public class EditorScope : BaseLifetimeScope
{
	public EditorScope(BaseLifetimeScope parentScope) : base(nameof(EditorScope), parentScope) { }

	protected override Task OnScopeStart()
	{
		Scope.Resolve<EditorEntryPoint>().Start();
		
		return Task.CompletedTask;
	}

	protected override void ConfigureContainer(ContainerBuilder b)
	{
		b.RegisterSingleton<EditorEntryPoint>();
		
		ConfigureViews(b);
	}

	private void ConfigureViews(ContainerBuilder b)
	{
		b.RegisterType<ProjectEditorWindowViewModel>();
	}
}