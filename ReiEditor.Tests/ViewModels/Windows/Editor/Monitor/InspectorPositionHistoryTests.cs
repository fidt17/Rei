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
public sealed class InspectorPositionHistoryTests
{
    [AvaloniaFact]
    public void EachConfirmedCoordinateSendsOnePositionWriteAndDraftsSendNothing()
    {
        var requests = new List<SetEntityDataRequest>();
        using var c = new InspectorPositionFixture(new TestEntityApi { OnSetData = requests.Add });
        foreach (var axis in new[] { "X", "Y", "Z" })
        {
            requests.Clear();
            var input = new ReiTextBox { DataContext = c.ViewModel, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(axis) { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "1";
            input.TextInternal = "12";
            input.TextInternal = "1.25e2";
            Assert.Empty(requests);
            input.LoseFocus();
            Dispatcher.UIThread.RunJobs();
            var request = Assert.Single(requests);
            Assert.True(request.RecordPositionChange);
            Assert.Equal(c.Entity.Id, request.SceneId);
            var behaviour = Assert.Single(request.Behaviours);
            Assert.Equal(InspectorPositionFixture.TRANSFORM_ID, behaviour[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
            var payload = JObject.FromObject(behaviour["_position"]!);
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
        Assert.Empty(c.Logger.Entries);
    }

    [AvaloniaFact]
    public void ScaleAndRotationKeepTheirExistingWritesWithoutRecordingHistory()
    {
        var requests = new List<SetEntityDataRequest>();
        using var c = new InspectorPositionFixture(new TestEntityApi { OnSetData = requests.Add });
        foreach (var name in new[] { "_scale", "_rotation" })
        {
            using var vm = new Vector3PropertyViewModel(c.Transform.GetProperty(name));
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding("X") { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "2";
            input.LoseFocus();
        }
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.False(request.RecordPositionChange));
        Assert.Equal(0f, c.ViewModel.X);
    }

    [AvaloniaFact]
    public void EngineSnapshotsDoNotEchoAndPlayOrInactiveEditsDoNotRecordPosition()
    {
        var requests = new List<SetEntityDataRequest>();
        using var c = new InspectorPositionFixture(new TestEntityApi { OnSetData = requests.Add });
        c.Apply(new GetEntityDataResponse
        {
            Name = c.Entity.Name,
            Behaviours = [new() { ["REI_TYPE"] = "Transform", ["_position"] = JObject.FromObject(new { x = 1f, y = 2f, z = 3f }) }]
        });
        Assert.Equal(1f, c.ViewModel.X);
        Assert.Empty(requests);
        var input = new ReiTextBox { DataContext = c.ViewModel, IsFloat = true };
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
        Assert.False(Assert.Single(requests).RecordPositionChange);
        c.Runner.Active.Value = false;
        input.TextInternal = "9";
        input.LoseFocus();
        Assert.Single(requests);
        Assert.Empty(c.Logger.Entries);
    }
}
