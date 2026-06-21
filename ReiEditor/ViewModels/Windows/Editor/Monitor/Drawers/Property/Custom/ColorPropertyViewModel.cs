using System.Globalization;
using Avalonia.Media;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Render;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class ColorPropertyViewModel : BaseCustomPropertyViewModel
{
    private bool _isSyncing;
    private bool _suppressComponentValueChanged;

    private float _r;
    public float R
    {
        get => _r;
        set
        {
            var clamped = ColorConversionUtility.Clamp01(value);
            if (!SetField(ref _r, clamped) || _isSyncing) return;
            ApplyFromRgba();
        }
    }

    private float _g;
    public float G
    {
        get => _g;
        set
        {
            var clamped = ColorConversionUtility.Clamp01(value);
            if (!SetField(ref _g, clamped) || _isSyncing) return;
            ApplyFromRgba();
        }
    }

    private float _b;
    public float B
    {
        get => _b;
        set
        {
            var clamped = ColorConversionUtility.Clamp01(value);
            if (!SetField(ref _b, clamped) || _isSyncing) return;
            ApplyFromRgba();
        }
    }

    private float _a = 1f;
    public float A
    {
        get => _a;
        set
        {
            var clamped = ColorConversionUtility.Clamp01(value);
            if (!SetField(ref _a, clamped) || _isSyncing) return;
            ApplyFromRgba();
        }
    }

    private float _h;
    public float H
    {
        get => _h;
        set
        {
            var clamped = ColorConversionUtility.ClampHue(value);
            if (!SetField(ref _h, clamped) || _isSyncing) return;
            ApplyFromHsv();
        }
    }

    private float _s = 1f;
    public float S
    {
        get => _s;
        set
        {
            var clamped = ColorConversionUtility.Clamp01(value);
            if (!SetField(ref _s, clamped) || _isSyncing) return;
            ApplyFromHsv();
        }
    }

    private float _v = 1f;
    public float V
    {
        get => _v;
        set
        {
            var clamped = ColorConversionUtility.Clamp01(value);
            if (!SetField(ref _v, clamped) || _isSyncing) return;
            ApplyFromHsv();
        }
    }

    private string _hex = "#FFFFFFFF";
    public string Hex
    {
        get => _hex;
        set
        {
            if (!SetField(ref _hex, value) || _isSyncing) return;

            if (!ColorConversionUtility.TryParseHex(value, out var r, out var g, out var b, out var a))
            {
                return;
            }

            RunSync(() =>
            {
                SetField(ref _r, r, nameof(R));
                SetField(ref _g, g, nameof(G));
                SetField(ref _b, b, nameof(B));
                SetField(ref _a, a, nameof(A));
                ColorConversionUtility.RgbToHsv(r, g, b, out var h, out var s, out var v);
                SetField(ref _h, h, nameof(H));
                SetField(ref _s, s, nameof(S));
                SetField(ref _v, v, nameof(V));
                SetField(ref _hex, ColorConversionUtility.ToHex(r, g, b, a));
                SetField(ref _colorHex, ColorConversionUtility.ToHex(r, g, b, 1f), nameof(ColorHex));
                SetField(ref _previewBrush, new SolidColorBrush(ColorConversionUtility.FromRgba01(r, g, b, 1f)), nameof(PreviewBrush));
            });

            PushRgbaToProperty();
        }
    }

    private string _colorHex = "#FFFFFF";
    public string ColorHex
    {
        get => _colorHex;
        private set => SetField(ref _colorHex, value);
    }

    private IBrush _previewBrush = Brushes.White;
    public IBrush PreviewBrush
    {
        get => _previewBrush;
        private set => SetField(ref _previewBrush, value);
    }

    public ColorPropertyViewModel() { }

    public ColorPropertyViewModel(SerializedProperty property) : base(property)
    {
        GetNestedProperty("r")!.ValueChangedEvent += HandleComponentValueChanged;
        GetNestedProperty("g")!.ValueChangedEvent += HandleComponentValueChanged;
        GetNestedProperty("b")!.ValueChangedEvent += HandleComponentValueChanged;
        GetNestedProperty("a")!.ValueChangedEvent += HandleComponentValueChanged;
    }

    public override void Dispose()
    {
        base.Dispose();

        GetNestedProperty("r")!.ValueChangedEvent -= HandleComponentValueChanged;
        GetNestedProperty("g")!.ValueChangedEvent -= HandleComponentValueChanged;
        GetNestedProperty("b")!.ValueChangedEvent -= HandleComponentValueChanged;
        GetNestedProperty("a")!.ValueChangedEvent -= HandleComponentValueChanged;
    }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        if (_suppressComponentValueChanged) return;

        var r = ConvertToFloat(GetNestedProperty("r")?.Value, 0f);
        var g = ConvertToFloat(GetNestedProperty("g")?.Value, 0f);
        var b = ConvertToFloat(GetNestedProperty("b")?.Value, 0f);
        var a = ConvertToFloat(GetNestedProperty("a")?.Value, 1f);

        RunSync(() =>
        {
            SetField(ref _r, ColorConversionUtility.Clamp01(r), nameof(R));
            SetField(ref _g, ColorConversionUtility.Clamp01(g), nameof(G));
            SetField(ref _b, ColorConversionUtility.Clamp01(b), nameof(B));
            SetField(ref _a, ColorConversionUtility.Clamp01(a), nameof(A));

            ColorConversionUtility.RgbToHsv(_r, _g, _b, out var h, out var s, out var v);
            if (s <= 0.0001f) h = _h;
            SetField(ref _h, h, nameof(H));
            SetField(ref _s, s, nameof(S));
            SetField(ref _v, v, nameof(V));

            SetField(ref _hex, ColorConversionUtility.ToHex(_r, _g, _b, _a), nameof(Hex));
            SetField(ref _colorHex, ColorConversionUtility.ToHex(_r, _g, _b, 1f), nameof(ColorHex));
            SetField(ref _previewBrush, new SolidColorBrush(ColorConversionUtility.FromRgba01(_r, _g, _b, 1f)), nameof(PreviewBrush));
        });
    }

    private void HandleComponentValueChanged(object? _)
    {
        if (_suppressComponentValueChanged) return;
        Dispatcher.UIThread.Execute(() => HandlePropertyValueChangedEvent(null));
    }

    private void ApplyFromRgba()
    {
        PushRgbaToProperty();

        RunSync(() =>
        {
            ColorConversionUtility.RgbToHsv(_r, _g, _b, out var h, out var s, out var v);
            SetField(ref _h, h, nameof(H));
            SetField(ref _s, s, nameof(S));
            SetField(ref _v, v, nameof(V));
            SetField(ref _hex, ColorConversionUtility.ToHex(_r, _g, _b, _a), nameof(Hex));
            SetField(ref _colorHex, ColorConversionUtility.ToHex(_r, _g, _b, 1f), nameof(ColorHex));
            SetField(ref _previewBrush, new SolidColorBrush(ColorConversionUtility.FromRgba01(_r, _g, _b, 1f)), nameof(PreviewBrush));
        });
    }

    private void ApplyFromHsv()
    {
        ColorConversionUtility.HsvToRgb(_h, _s, _v, out var r, out var g, out var b);

        RunSync(() =>
        {
            SetField(ref _r, r, nameof(R));
            SetField(ref _g, g, nameof(G));
            SetField(ref _b, b, nameof(B));
            SetField(ref _hex, ColorConversionUtility.ToHex(_r, _g, _b, _a), nameof(Hex));
            SetField(ref _colorHex, ColorConversionUtility.ToHex(_r, _g, _b, 1f), nameof(ColorHex));
            SetField(ref _previewBrush, new SolidColorBrush(ColorConversionUtility.FromRgba01(_r, _g, _b, 1f)), nameof(PreviewBrush));
        });

        PushRgbaToProperty();
    }

    private void PushRgbaToProperty()
    {
        var rProperty = GetNestedProperty("r");
        var gProperty = GetNestedProperty("g");
        var bProperty = GetNestedProperty("b");
        var aProperty = GetNestedProperty("a");
        if (rProperty == null || gProperty == null || bProperty == null || aProperty == null) return;

        _suppressComponentValueChanged = true;
        try
        {
            rProperty.Value = _r;
            gProperty.Value = _g;
            bProperty.Value = _b;
            aProperty.Value = _a;
        }
        finally
        {
            _suppressComponentValueChanged = false;
        }
    }

    private void RunSync(System.Action action)
    {
        _isSyncing = true;
        try
        {
            action();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private static float ConvertToFloat(object? value, float defaultValue)
    {
        if (value is null) return defaultValue;
        if (value is JToken token) value = token.ToObject<object?>();
        if (value is float f) return f;
        if (value is double d) return (float)d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is decimal dec) return (float)dec;

        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedInvariant))
        {
            return parsedInvariant;
        }

        if (float.TryParse(text, out var parsedCurrent))
        {
            return parsedCurrent;
        }

        return defaultValue;
    }
}
