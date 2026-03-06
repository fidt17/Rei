using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Views.Controls.ColorPicker;

public class ColorWheelPicker : Control
{
    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<ColorWheelPicker, double>(nameof(Hue), 0d, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<ColorWheelPicker, double>(nameof(Saturation), 1d, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> BrightnessProperty =
        AvaloniaProperty.Register<ColorWheelPicker, double>(nameof(Brightness), 1d, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private enum DragMode
    {
        None,
        Hue,
        SaturationValue,
    }

    private DragMode _dragMode;
    private WriteableBitmap? _svBitmap;
    private int _svBitmapWidth;
    private int _svBitmapHeight;
    private float _svBitmapHue = -1f;

    static ColorWheelPicker()
    {
        AffectsRender<ColorWheelPicker>(HueProperty, SaturationProperty, BrightnessProperty, BoundsProperty);
    }

    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double Brightness
    {
        get => GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!TryGetLayout(out var layout)) return;

        DrawHueWheel(context, layout);
        DrawSvSquare(context, layout);
        DrawHandles(context, layout);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!TryGetLayout(out var layout)) return;

        var point = e.GetPosition(this);

        if (layout.SvRect.Contains(point))
        {
            _dragMode = DragMode.SaturationValue;
            UpdateSvFromPoint(point, layout);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (IsPointOnHueRing(point, layout))
        {
            _dragMode = DragMode.Hue;
            UpdateHueFromPoint(point, layout);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!TryGetLayout(out var layout)) return;
        if (_dragMode == DragMode.None) return;

        var point = e.GetPosition(this);
        if (_dragMode == DragMode.Hue)
        {
            UpdateHueFromPoint(point, layout);
        }
        else if (_dragMode == DragMode.SaturationValue)
        {
            UpdateSvFromPoint(point, layout);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _dragMode = DragMode.None;
        e.Pointer.Capture(null);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _svBitmap?.Dispose();
        _svBitmap = null;
        _svBitmapWidth = 0;
        _svBitmapHeight = 0;
        _svBitmapHue = -1f;
    }

    private void DrawHueWheel(DrawingContext context, LayoutInfo layout)
    {
        const int segments = 240;

        for (var i = 0; i < segments; i++)
        {
            var hueA = i * 360.0 / segments;
            var hueB = (i + 1) * 360.0 / segments;
            var angleA = DegreesToRadians(hueA);
            var angleB = DegreesToRadians(hueB);

            var p1 = new Point(
                layout.Center.X + Math.Cos(angleA) * layout.RingMidRadius,
                layout.Center.Y + Math.Sin(angleA) * layout.RingMidRadius);
            var p2 = new Point(
                layout.Center.X + Math.Cos(angleB) * layout.RingMidRadius,
                layout.Center.Y + Math.Sin(angleB) * layout.RingMidRadius);

            ColorConversionUtility.HsvToRgb((float)hueA, 1f, 1f, out var r, out var g, out var b);
            var color = ColorConversionUtility.FromRgba01(r, g, b, 1f);
            var pen = new Pen(new SolidColorBrush(color), layout.RingThickness + 1f);
            context.DrawLine(pen, p1, p2);
        }
    }

    private void DrawSvSquare(DrawingContext context, LayoutInfo layout)
    {
        var targetWidth = Math.Max(2, (int)Math.Round(layout.SvRect.Width));
        var targetHeight = Math.Max(2, (int)Math.Round(layout.SvRect.Height));
        var hue = ColorConversionUtility.ClampHue((float)Hue);

        EnsureSvBitmap(targetWidth, targetHeight, hue);
        if (_svBitmap != null)
        {
            context.DrawImage(_svBitmap, new Rect(0, 0, targetWidth, targetHeight), layout.SvRect);
        }

        context.DrawRectangle(null, new Pen(Brushes.Gray, 1), layout.SvRect);
    }

    private void DrawHandles(DrawingContext context, LayoutInfo layout)
    {
        var hue = ColorConversionUtility.ClampHue((float)Hue);
        var hueAngle = DegreesToRadians(hue);
        var huePoint = new Point(
            layout.Center.X + Math.Cos(hueAngle) * layout.RingMidRadius,
            layout.Center.Y + Math.Sin(hueAngle) * layout.RingMidRadius);

        DrawHandle(context, huePoint, 5);

        var saturation = ColorConversionUtility.Clamp01((float)Saturation);
        var value = ColorConversionUtility.Clamp01((float)Brightness);
        var valueVisual = BrightnessToVisualY(value);
        var svPoint = new Point(
            layout.SvRect.X + saturation * layout.SvRect.Width,
            layout.SvRect.Y + valueVisual * layout.SvRect.Height);

        DrawHandle(context, svPoint, 6);
    }

    private static void DrawHandle(DrawingContext context, Point point, double radius)
    {
        context.DrawEllipse(Brushes.Transparent, new Pen(Brushes.Black, 2), point, radius, radius);
        context.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, 1), point, radius + 1.5, radius + 1.5);
    }

    private bool IsPointOnHueRing(Point point, LayoutInfo layout)
    {
        var dx = point.X - layout.Center.X;
        var dy = point.Y - layout.Center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        return distance >= layout.InnerRingRadius && distance <= layout.OuterRingRadius;
    }

    private void UpdateHueFromPoint(Point point, LayoutInfo layout)
    {
        var angle = Math.Atan2(point.Y - layout.Center.Y, point.X - layout.Center.X);
        var degrees = angle * 180.0 / Math.PI;
        if (degrees < 0) degrees += 360.0;

        SetCurrentValue(HueProperty, degrees);
    }

    private void UpdateSvFromPoint(Point point, LayoutInfo layout)
    {
        var x = point.X;
        var y = point.Y;
        if (x < layout.SvRect.Left) x = layout.SvRect.Left;
        if (x > layout.SvRect.Right) x = layout.SvRect.Right;
        if (y < layout.SvRect.Top) y = layout.SvRect.Top;
        if (y > layout.SvRect.Bottom) y = layout.SvRect.Bottom;

        var saturation = (x - layout.SvRect.Left) / layout.SvRect.Width;
        var normalizedY = (y - layout.SvRect.Top) / layout.SvRect.Height;
        var value = VisualYToBrightness(normalizedY);

        SetCurrentValue(SaturationProperty, saturation);
        SetCurrentValue(BrightnessProperty, value);
    }

    private bool TryGetLayout(out LayoutInfo layout)
    {
        layout = default;

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0) return false;

        var minSize = Math.Min(width, height);
        var outerRadius = minSize * 0.5 - 6;
        if (outerRadius <= 0) return false;

        var ringThickness = Math.Max(14, outerRadius * 0.16);
        var innerRingRadius = outerRadius - ringThickness;
        var ringMidRadius = (outerRadius + innerRingRadius) * 0.5;

        var squareHalf = innerRingRadius / Math.Sqrt(2) - 4;
        if (squareHalf <= 0) return false;

        var center = new Point(width * 0.5, height * 0.5);
        var svRect = new Rect(
            center.X - squareHalf,
            center.Y - squareHalf,
            squareHalf * 2,
            squareHalf * 2);

        layout = new LayoutInfo(center, outerRadius, innerRingRadius, ringMidRadius, ringThickness, svRect);
        return true;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180.0;

    private static double VisualYToBrightness(double normalizedY)
    {
        if (normalizedY <= 0) return 1;
        if (normalizedY >= 1) return 0;
        return 1.0 - normalizedY;
    }

    private static double BrightnessToVisualY(double brightness)
    {
        if (brightness >= 1) return 0;
        if (brightness <= 0) return 1;
        return 1.0 - brightness;
    }

    private void EnsureSvBitmap(int width, int height, float hue)
    {
        var mustRecreate = _svBitmap == null || _svBitmapWidth != width || _svBitmapHeight != height;
        var mustRepaint = mustRecreate || Math.Abs(_svBitmapHue - hue) > 0.01f;
        if (!mustRepaint) return;

        if (mustRecreate)
        {
            _svBitmap?.Dispose();
            _svBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
            _svBitmapWidth = width;
            _svBitmapHeight = height;
        }

        if (_svBitmap == null) return;

        using var locked = _svBitmap.Lock();
        var rowBytes = locked.RowBytes;
        var buffer = new byte[rowBytes * height];

        for (var y = 0; y < height; y++)
        {
            var v = 1f - (float)y / (height - 1);
            for (var x = 0; x < width; x++)
            {
                var s = (float)x / (width - 1);
                ColorConversionUtility.HsvToRgb(hue, s, v, out var r, out var g, out var b);

                var index = y * rowBytes + x * 4;
                buffer[index + 0] = (byte)Math.Round(b * 255f);
                buffer[index + 1] = (byte)Math.Round(g * 255f);
                buffer[index + 2] = (byte)Math.Round(r * 255f);
                buffer[index + 3] = 255;
            }
        }

        Marshal.Copy(buffer, 0, locked.Address, buffer.Length);
        _svBitmapHue = hue;
    }

    private readonly record struct LayoutInfo(
        Point Center,
        double OuterRingRadius,
        double InnerRingRadius,
        double RingMidRadius,
        double RingThickness,
        Rect SvRect);
}
