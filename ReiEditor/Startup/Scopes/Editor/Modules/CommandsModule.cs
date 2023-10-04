using Autofac;
using ReiEditor.Views.Windows.Editor.Commands;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class CommandsModule : Module
{
	protected override void Load(ContainerBuilder b)
	{
		b.RegisterType<SaveProjectCommand>();
	}
}