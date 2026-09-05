using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.Services.TransformationControls;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Windows.Editor.Playmode;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Playmode;

/// <summary>Verifies transformation toggles, explicit actions and dispatched state notifications.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Playmode")]
public sealed class TransformationControlSettingsViewModelTests
{
    private sealed class TestControls : ITransformationControlsService
    {
        public event Action? StateChanged;
        public bool CanUseLocalSpace => false;
        public bool CanUseWorldSpace => true;
        public bool IsLocalSpace { get; private set; }
        public bool IsWorldSpace => !IsLocalSpace;
        public bool CanUseRectTransformMode => false;
        public bool EngineRunning => true;
        public TransformationMode Mode { get; private set; }
        public List<TransformationMode> Requests { get; } = [];
        public void SetWorldSpace() => IsLocalSpace = false;
        public void SetLocalSpace() => IsLocalSpace = true;
        public void SetMode(TransformationMode mode) { Mode = mode; Requests.Add(mode); }
        public void PublishChanged() => StateChanged?.Invoke();
    }

    /// <summary>Checking a toggle selects its mode; unchecking only refreshes the binding and preserves selection.</summary>
    [AvaloniaTheory]
    [InlineData(TransformationMode.Movement)]
    [InlineData(TransformationMode.Scale)]
    [InlineData(TransformationMode.Rotation)]
    [InlineData(TransformationMode.RectTransform)]
    public void ToggleCannotUnselectCurrentMode(TransformationMode mode)
    {
        var controls = new TestControls();
        using var vm = new TransformationControlSettingsViewModel(controls);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        void Toggle(bool value)
        {
            switch (mode)
            {
                case TransformationMode.Movement: vm.MovementMode = value; break;
                case TransformationMode.Scale: vm.ScaleMode = value; break;
                case TransformationMode.Rotation: vm.RotationMode = value; break;
                default: vm.RectTransformMode = value; break;
            }
        }
        Toggle(true);
        Toggle(false);
        Assert.Equal(mode, Assert.Single(controls.Requests));
        Assert.Equal(mode, controls.Mode);
        Assert.Single(changed);
        Assert.Equal(mode == TransformationMode.Movement, vm.MovementMode);
        Assert.Equal(mode == TransformationMode.Scale, vm.ScaleMode);
        Assert.Equal(mode == TransformationMode.Rotation, vm.RotationMode);
        Assert.Equal(mode == TransformationMode.RectTransform, vm.RectTransformMode);
    }

    /// <summary>Actions route to the service, state updates notify all exposed properties and disposal stops later notifications.</summary>
    [AvaloniaFact]
    public async Task ActionsAndStateNotificationsUseServiceAndDispatcher()
    {
        var controls = new TestControls();
        using var vm = new TransformationControlSettingsViewModel(controls);
        vm.SetMovementMode(); vm.SetScaleMode(); vm.SetRotationMode(); vm.SetRectTransformMode();
        Assert.Equal(new[] { TransformationMode.Movement, TransformationMode.Scale, TransformationMode.Rotation, TransformationMode.RectTransform }, controls.Requests);
        vm.SetLocalSpace();
        Assert.True(vm.IsLocalSpace);
        vm.SetWorldSpace();
        Assert.True(vm.IsWorldSpace);
        Assert.False(vm.CanUseLocalSpace);
        Assert.True(vm.CanUseWorldSpace);
        Assert.False(vm.CanUseRectTransformMode);
        Assert.True(vm.EngineRunning);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        controls.PublishChanged();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.Equal(new[] { "CanUseLocalSpace", "CanUseWorldSpace", "IsLocalSpace", "IsWorldSpace", "CanUseRectTransformMode", "EngineRunning", "MovementMode", "ScaleMode", "RotationMode", "RectTransformMode" }, changed);
        vm.Dispose();
        changed.Clear();
        controls.PublishChanged();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.Empty(changed);
    }
}
