using Autofac;
using ReiEditor.ViewModels.Windows.Editor.Monitor;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class MonitorModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MonitorWindowViewModel>();
        builder.RegisterType<EntityMonitorDrawerViewModel>();
        builder.RegisterType<AssetMonitorDrawerViewModel>();
        builder.RegisterType<MaterialMonitorDrawerViewModel>();

        builder.RegisterType<EntityInfoComponentDrawerViewModel>();
        builder.RegisterType<BehaviourComponentDrawerViewModel>();
    }
}
