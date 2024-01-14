using Autofac;
using ReiEditor.ViewModels.Windows.Editor.Monitor;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class MonitorModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MonitorWindowViewModel>();
        builder.RegisterType<EntityMonitorDrawerViewModel>();
    }
}