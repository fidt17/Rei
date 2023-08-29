using System.Threading.Tasks;
using Autofac;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Build;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor;

namespace ReiEditor.Startup.Scopes.Editor;

public class EditorScope : BaseLifetimeScope
{
	public EditorScope(BaseLifetimeScope parentScope) : base(nameof(EditorScope), parentScope) { }

	protected override Task OnScopeStart()
	{
		Scope.Resolve<EditorEntryPoint>().Start();
		Scope.Resolve<IBuildService>().BuildProject(BuildConfigurationEnum.EditorDebug);
		
		return Task.CompletedTask;
	}

	protected override void ConfigureContainer(ContainerBuilder b)
	{
		b.RegisterSingleton<EditorEntryPoint>();
		
		b.RegisterSingleton<ResourceService>().As<IResourceService>();

		b.RegisterModule<EditorConsoleModule>();
		b.RegisterModule<PlaymodeModule>();
		b.RegisterModule<SettingsModule>();
		b.RegisterModule<BuildModule>();
		
		ConfigureViews(b);
	}

	private void ConfigureViews(ContainerBuilder b)
	{
		b.RegisterType<ProjectEditorWindowViewModel>();
	}
}