using System.Threading.Tasks;
using Autofac;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor;
using ReiEditor.ViewModels.Windows.Editor.Console;

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
		b.RegisterSingleton<EditorConsoleService>().As<IEditorConsoleService>();
		
		b.RegisterSingleton<EditorEntryPoint>();
		
		ConfigureViews(b);
	}

	private void ConfigureViews(ContainerBuilder b)
	{
		b.RegisterType<ProjectEditorWindowViewModel>();
		b.RegisterType<ConsoleEditorWindowViewModel>();
	}
}