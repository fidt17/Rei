using Avalonia;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;
using ReiEditor.Views.Controls.TextBoxes;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "EditorCommands")]
public sealed class InspectorTransformHistoryTests
{
    [AvaloniaFact]
    public void EachConfirmedTransformCoordinateSendsOneWriteAndDraftsSendNothing()
    {
        var requests = new List<SetEntityDataRequest>();
        using var c = new InspectorTransformFixture(new TestEntityApi { OnSetData = requests.Add });
        foreach (var name in new[] { "_position", "_scale", "_rotation" })
        {
            using var vm = new Vector3PropertyViewModel(c.Transform.GetProperty(name));
            foreach (var axis in new[] { "X", "Y", "Z" })
            {
                requests.Clear();
                var input = new ReiTextBox { DataContext = vm, IsFloat = true };
                using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(axis) { Mode = BindingMode.TwoWay });
                Dispatcher.UIThread.RunJobs();
                input.TextInternal = "1";
                input.TextInternal = "12";
                input.TextInternal = "1.25e2";
                Assert.Empty(requests);
                input.LoseFocus();
                Dispatcher.UIThread.RunJobs();
                var request = Assert.Single(requests);
                Assert.True(request.RecordTransformChange);
                Assert.Equal(c.Entity.Id, request.SceneId);
                var behaviour = Assert.Single(request.Behaviours);
                Assert.Equal(InspectorTransformFixture.TRANSFORM_ID, behaviour[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
                var payload = JObject.FromObject(behaviour[name]!);
                Assert.Equal(125f, payload["Value"]![axis.ToLowerInvariant()]!["Value"]!.Value<float>());
                input.TextInternal = "125.0";
                input.LoseFocus();
                foreach (var invalid in new[] { "invalid number", "NaN", "Infinity", "1e100" })
                {
                    input.TextInternal = invalid;
                    input.LoseFocus();
                    Dispatcher.UIThread.RunJobs();
                    Assert.Single(requests);
                    Assert.Equal("125", input.TextInternal);
                }
            }
        }
        Assert.Empty(c.Logger.Entries);
    }

    [AvaloniaFact]
    public void OtherTransformPropertiesDoNotRequestHistory()
    {
        var requests = new List<SetEntityDataRequest>();
        using var c = new InspectorTransformFixture(new TestEntityApi { OnSetData = requests.Add });
        c.Transform.GetProperty("_parent").Value = 1;
        c.Transform.GetProperty("_order").Value = 2;
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.False(request.RecordTransformChange));
        Assert.Equal(0f, c.ViewModel.X);
    }

    [AvaloniaFact]
    public void TransformSnapshotsDoNotEchoAndPlayOrInactiveEditsDoNotRequestHistory()
    {
        foreach (var name in new[] { "_position", "_scale", "_rotation" })
        {
            var requests = new List<SetEntityDataRequest>();
            using var c = new InspectorTransformFixture(new TestEntityApi { OnSetData = requests.Add });
            using var vm = new Vector3PropertyViewModel(c.Transform.GetProperty(name));
            c.Apply(new GetEntityDataResponse
            {
                Name = c.Entity.Name,
                Behaviours = [new() { ["REI_TYPE"] = "Transform", [name] = JObject.FromObject(new { x = 1f, y = 2f, z = 3f }) }]
            });
            Assert.Equal(1f, vm.X);
            Assert.Empty(requests);
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding("X") { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "1";
            input.LoseFocus();
            Assert.Empty(requests);
            c.Runner.EditorActive.Value = false;
            c.Runner.PlaymodeActive.Value = true;
            c.Runner.ActiveMode = EngineRunMode.PlayMode;
            input.TextInternal = "5";
            input.LoseFocus();
            Assert.False(Assert.Single(requests).RecordTransformChange);
            c.Runner.Active.Value = false;
            input.TextInternal = "9";
            input.LoseFocus();
            Assert.Single(requests);
            Assert.Empty(c.Logger.Entries);
        }
    }
}
