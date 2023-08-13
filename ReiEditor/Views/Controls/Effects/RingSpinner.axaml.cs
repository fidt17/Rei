using System;
using Avalonia.Collections;
using Avalonia.Controls;

namespace ReiEditor.Views.Controls.Effects;

public partial class RingSpinner : UserControl
{
	public RingSpinner()
	{
		InitializeComponent();
		UpdateSpinnerParts();
	}

    private void UpdateSpinnerParts()
    {
        var circumference = Math.PI * (EllipseSpinner.Width / EllipseSpinner.StrokeThickness);
        const double fillPart = 0.65;
        var lineLength = circumference * fillPart;
        var emptyPart = circumference - lineLength;
        EllipseSpinner.StrokeDashArray = new AvaloniaList<double>(lineLength, emptyPart);
    }
}