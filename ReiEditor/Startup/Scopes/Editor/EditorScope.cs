using System.Threading.Tasks;
using Autofac;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor;

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
		b.RegisterSingleton<AssetsService>().As<IAssetsService>();
		b.RegisterSingleton<SceneManagementService>().As<ISceneManagementService>();
		b.RegisterSingleton<ProjectSetupService>().As<IProjectSetupService>();

		b.RegisterModule<CommandsModule>();
		b.RegisterModule<EditorConsoleModule>();
		b.RegisterModule<HierarchyModule>();
		b.RegisterModule<PlaymodeModule>();
		b.RegisterModule<SettingsModule>();
		b.RegisterModule<BuildModule>();
		b.RegisterModule<StatusBarModule>();
		
		ConfigureViews(b);
	}

	private void ConfigureViews(ContainerBuilder b)
	{
		b.RegisterType<ProjectEditorWindowViewModel>();
	}
}