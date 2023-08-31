using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReiEditor.Views.Controls.Effects;

public partial class RingSpinner : UserControl
{
	public static readonly StyledProperty<IBrush> SpinnerColorProperty = AvaloniaProperty.Register<RingSpinner, IBrush>(nameof(SpinnerColor));

	public IBrush SpinnerColor
	{
		get => GetValue(SpinnerColorProperty);
		set => SetValue(SpinnerColorProperty, value);
	}

	public static readonly StyledProperty<float> ThicknessProperty = AvaloniaProperty.Register<RingSpinner, float>(
		"Thickness");

	public float Thickness
	{
		get => GetValue(ThicknessProperty);
		set => SetValue(ThicknessProperty, value);
	}

	public static readonly StyledProperty<AvaloniaList<double>> StokeDashArrayProperty = AvaloniaProperty.Register<RingSpinner, AvaloniaList<double>>(
		"StokeDashArray");

	public AvaloniaList<double> StokeDashArray
	{
		get => GetValue(StokeDashArrayProperty);
		set => SetValue(StokeDashArrayProperty, value);
	}
	
	public RingSpinner()
	{
		InitializeComponent();
	}
}