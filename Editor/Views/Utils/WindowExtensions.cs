using System;
using Avalonia;
using Avalonia.Controls;

namespace Editor.Views.Utils;

public static class WindowExtensions
{
	public static void CenterWindow(this Window window)
	{
		var mainScreen = window.Screens.ScreenFromWindow(window);
		if (mainScreen == null) throw new Exception("Main screen is missing");
		
		var xPos = (int)(mainScreen.Bounds.Width * 0.5 - window.Bounds.Width * 0.5);
		var yPos = (int)(mainScreen.Bounds.Height * 0.5 - window.Bounds.Height * 0.5);
		window.Position = new PixelPoint(xPos, yPos);
	}
}