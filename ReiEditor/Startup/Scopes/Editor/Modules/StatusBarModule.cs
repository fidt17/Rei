using Autofac;
using ReiEditor.ViewModels.Windows.Editor.StatusBar;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class StatusBarModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<StatusBarViewModel>();
    }
}