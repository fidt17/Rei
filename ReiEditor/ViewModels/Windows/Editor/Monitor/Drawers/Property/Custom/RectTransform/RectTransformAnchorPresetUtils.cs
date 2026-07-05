using System;
using System.Collections.Generic;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.RectTransform;

public static class RectTransformAnchorPresetUtils
{
    private const float EPSILON = 0.0001f;

    public static IReadOnlyList<RectTransformAnchorPreset> Presets { get; } = CreatePresets();

    public static RectTransformAnchorPreset? FindMatchingPreset(float minX, float minY, float maxX, float maxY)
    {
        foreach (var preset in Presets)
        {
            if (!Approximately(preset.MinX, minX)) continue;
            if (!Approximately(preset.MinY, minY)) continue;
            if (!Approximately(preset.MaxX, maxX)) continue;
            if (!Approximately(preset.MaxY, maxY)) continue;

            return preset;
        }

        return null;
    }

    public static bool IsStretch(float min, float max) => !Approximately(min, max);

    public static bool IsMatching(RectTransformAnchorPreset preset, float minX, float minY, float maxX, float maxY)
    {
        return Approximately(preset.MinX, minX)
               && Approximately(preset.MinY, minY)
               && Approximately(preset.MaxX, maxX)
               && Approximately(preset.MaxY, maxY);
    }

    private static IReadOnlyList<RectTransformAnchorPreset> CreatePresets()
    {
        var horizontal = new[]
        {
            new AxisPreset("Left", "L", 0f, 0f),
            new AxisPreset("Center", "C", 0.5f, 0.5f),
            new AxisPreset("Right", "R", 1f, 1f),
            new AxisPreset("Stretch X", "S", 0f, 1f)
        };
        var vertical = new[]
        {
            new AxisPreset("Top", "T", 1f, 1f),
            new AxisPreset("Middle", "M", 0.5f, 0.5f),
            new AxisPreset("Bottom", "B", 0f, 0f),
            new AxisPreset("Stretch Y", "S", 0f, 1f)
        };

        var presets = new List<RectTransformAnchorPreset>();
        foreach (var v in vertical)
        {
            foreach (var h in horizontal)
            {
                presets.Add(new RectTransformAnchorPreset(
                    GetDisplayName(h, v),
                    $"{v.Label}{h.Label}",
                    h.Min,
                    v.Min,
                    h.Max,
                    v.Max));
            }
        }

        return presets;
    }

    private static string GetDisplayName(AxisPreset horizontal, AxisPreset vertical)
    {
        var stretchX = horizontal.Min != horizontal.Max;
        var stretchY = vertical.Min != vertical.Max;

        if (stretchX && stretchY) return "Stretch Both";
        if (stretchX) return $"{vertical.Name} Stretch X";
        if (stretchY) return $"{horizontal.Name} Stretch Y";
        return $"{vertical.Name} {horizontal.Name}";
    }

    private static bool Approximately(float a, float b) => Math.Abs(a - b) < EPSILON;

    private readonly record struct AxisPreset(string Name, string Label, float Min, float Max);
}

public sealed class RectTransformAnchorPreset
{
    public string DisplayName { get; }
    public string ButtonText { get; }
    public float MinX { get; }
    public float MinY { get; }
    public float MaxX { get; }
    public float MaxY { get; }

    public RectTransformAnchorPreset(string displayName, string buttonText, float minX, float minY, float maxX, float maxY)
    {
        DisplayName = displayName;
        ButtonText = buttonText;
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }
}
