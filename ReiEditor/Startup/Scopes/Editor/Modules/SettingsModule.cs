using Autofac;
using ReiEditor.Models.EditorApp.SettingsWindow;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.Settings;
using ReiEditor.ViewModels.Windows.Editor.Settings.Commands;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class SettingsModule : Module
{
	protected override void Load(ContainerBuilder builder)
	{
		base.Load(builder);

		builder.RegisterSingleton<SettingsWindowService>().As<ISettingsWindowService>();
		builder.RegisterType<OpenSettingsWindowCommand>();
		builder.RegisterType<SetMsBuildLocationCommand>();

		builder.RegisterType<EditorSettingsWindowViewModel>();
	}
}