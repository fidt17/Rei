using Autofac;
using ReiEditor.Models.EditorApp.Diagnostics;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.Diagnostics;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public sealed class DiagnosticsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<DiagnosticsWindowService>().As<IDiagnosticsWindowService>();
        builder.RegisterType<DiagnosticsWindowViewModel>();
        builder.RegisterType<OpenDiagnosticsWindowCommand>();
    }
}
