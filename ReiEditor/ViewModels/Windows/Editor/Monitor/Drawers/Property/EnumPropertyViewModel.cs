using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class EnumPropertyViewModel : BasePropertyViewModel<int>
{
    public ObservableCollection<string> Options { get; } = new();
    
    #region SelectedValue

    private string _selectedValue = "";
    public string SelectedValue
    {
        get => _selectedValue;
        set
        {
            if (SetField(ref _selectedValue, value))
            {
                var enumIntValue = _serializableEnum.Options[value];
                Value = enumIntValue;
            }
        }
    }

    #endregion

    private readonly SerializableEnum _serializableEnum;

    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public EnumPropertyViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public EnumPropertyViewModel(SerializedProperty property, ISerializableObjectsRegistry serializableObjectsRegistry) : base(property)
    {
        _serializableObjectsRegistry = serializableObjectsRegistry;

        _serializableEnum = _serializableObjectsRegistry.GetEnum(property.SourceType.Split("::").Last())!;
        if (_serializableEnum == null) throw new Exception($"Could not find serializable enum data for type: {property.SourceType}");
        
        foreach (var option in _serializableEnum.Options)
        {
            Options.Add(option.Key);
        }

        PropertyChanged += HandlePropertyChanged;
        UpdateSelectedValue(_serializableEnum.Options.First(x => x.Value == Value).Key);
    }

    public override void Dispose()
    {
        PropertyChanged -= HandlePropertyChanged;
        base.Dispose();
    }

    protected override int ParseValue(object? value)
    {
        if (value is long l)
        {
            return (int) l;
        }

        if (value is int i)
        {
            return i;
        }
        
        throw new Exception($"Not supported value type: {value}");
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(Value)) return;
        if (_serializableEnum.Options.TryGetValue(_selectedValue, out var selectedValue) && selectedValue == Value) return;

        var matchingOption = _serializableEnum.Options.FirstOrDefault(x => x.Value == Value);
        if (matchingOption.Key != null) UpdateSelectedValue(matchingOption.Key);
    }

    private void UpdateSelectedValue(string selectedValue)
    {
        SetField(ref _selectedValue, selectedValue, nameof(SelectedValue));
    }
}
