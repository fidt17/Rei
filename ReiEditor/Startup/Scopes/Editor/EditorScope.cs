using System.Threading.Tasks;
using Autofac;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor;
using ReiEditor.ViewModels.Windows.Editor.Commands;

namespace ReiEditor.Startup.Scopes.Editor;

public class EditorScope : BaseLifetimeScope
{
	public EditorScope(BaseLifetimeScope parentScope) : base(nameof(EditorScope), parentScope) { }

	protected override async Task OnScopeStart() => await Scope.Resolve<EditorEntryPoint>().Start();

	protected override void ConfigureContainer(ContainerBuilder b)
	{
		b.RegisterSingleton<EditorEntryPoint>();
		
		b.RegisterSingleton<ResourceService>().As<IResourceService>();
		b.RegisterSingleton<EditorProceduresService>().As<IEditorProceduresService>();
		b.RegisterSingleton<ProjectSetupService>().As<IProjectSetupService>();
		
		b.RegisterType<SaveProjectCommand>();
		b.RegisterSingleton<AssetsService>().As<IAssetsService>();

		b.RegisterSingleton<SelectionService>().As<ISelectionService>();
		b.RegisterModule<EditorConsoleModule>();
		b.RegisterModule<SceneModule>();
		b.RegisterModule<MonitorModule>();
		b.RegisterModule<PlaymodeModule>();
		b.RegisterModule<SettingsModule>();
		b.RegisterModule<BuildModule>();
		b.RegisterModule<StatusBarModule>();
		
		ConfigureViewModules(b);
	}

	private void ConfigureViewModules(ContainerBuilder b)
	{
		b.RegisterType<ProjectEditorWindowViewModel>();
	}
}