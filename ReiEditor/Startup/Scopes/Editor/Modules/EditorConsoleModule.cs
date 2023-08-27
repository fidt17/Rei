using Autofac;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Console;
using Module = Autofac.Module;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class EditorConsoleModule : Module
{
	protected override void Load(ContainerBuilder builder)
	{
		builder.RegisterGeneric(typeof(EditorConsoleLogger<>)).As(typeof(ILogger<>));
		builder.RegisterSingleton<EditorConsoleService>().As<IEditorConsoleService>();
		
		ConfigureViews(builder);
	}

	private void ConfigureViews(ContainerBuilder builder)
	{
		builder.RegisterType<ConsoleEditorWindowViewModel>();
	}
}