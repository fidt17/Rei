using System.Threading.Tasks;
using Autofac;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor;
using ReiEditor.ViewModels.Windows.Editor.Console;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

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
		b.RegisterGeneric(typeof(EditorConsoleLogger<>)).As(typeof(ILogger<>));

		b.RegisterSingleton<ResourceService>().As<IResourceService>();

		b.RegisterSingleton<ClientDllManager>().As<IClientDllManager>();
		b.RegisterSingleton<ClientApi>().As<IClientApi>();
		
		b.RegisterSingleton<PlaymodeService>().As<IPlaymodeService>();
		b.RegisterSingleton<EditorConsoleService>().As<IEditorConsoleService>();
		
		b.RegisterSingleton<EditorEntryPoint>();
		
		ConfigureViews(b);
	}

	private void ConfigureViews(ContainerBuilder b)
	{
		b.RegisterType<ProjectEditorWindowViewModel>();
		
		ConfigurePlaymodePanel(b);
		
		b.RegisterType<ConsoleEditorWindowViewModel>();
	}
	
	private void ConfigurePlaymodePanel(ContainerBuilder b)
	{
		b.RegisterType<PlaymodePanelViewModel>();
		b.RegisterType<StartPlaymodeCommand>();
		b.RegisterType<StopPlaymodeCommand>();
	}
}