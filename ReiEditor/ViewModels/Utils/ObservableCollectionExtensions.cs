using System;
using System.Collections.ObjectModel;

namespace ReiEditor.ViewModels.Utils;

public static class ObservableCollectionExtensions
{
	public static void ClearAndDispose<T>(this ObservableCollection<T> collection)
	{
		foreach (var e in collection)
		{
			if (e is IDisposable d)
			{
				d.Dispose();
			}
		}
		
		collection.Clear();
	}
}