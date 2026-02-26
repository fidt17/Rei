using System;
using ReiEditor.Utils;

namespace ReiEditor.ViewModels.Controls.Assets;

public class AssetSearchItemViewModel
{
    public string Name { get; }
    public string FullPath { get; }
    public string AssetId { get; }
    public RelayCommand SelectCommand { get; }

    public AssetSearchItemViewModel(string name, string fullPath, string assetId, Action selectAction)
    {
        Name = name;
        FullPath = fullPath;
        AssetId = assetId;
        SelectCommand = new RelayCommand(selectAction);
    }
}
