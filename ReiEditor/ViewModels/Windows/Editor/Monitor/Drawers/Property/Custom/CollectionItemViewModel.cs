using System;
using System.Collections.ObjectModel;
using ReiEditor.Models.Services.Components;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Utils;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class CollectionItemViewModel : BaseViewModel
{
    public SerializedProperty Property { get; }
    public ObservableCollection<BaseViewModel> Value { get; } = new();
    public RelayCommand RemoveCommand { get; }

    public CollectionItemViewModel(SerializedProperty property, BaseViewModel itemViewModel, Action removeAction)
    {
        Property = property;
        Value.Add(itemViewModel);
        RemoveCommand = new RelayCommand(removeAction);
    }

    public override void Dispose()
    {
        base.Dispose();

        Value.ClearAndDispose();
    }
}
