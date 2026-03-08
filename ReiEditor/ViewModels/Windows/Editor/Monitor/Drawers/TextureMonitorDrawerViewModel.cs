using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Utils;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class TextureMonitorDrawerViewModel : BaseMonitorDrawer
{
    public string AssetName { get; }
    public string AssetId { get; }
    public string AssetIdLabel { get; }
    public string AssetSizeLabel { get; }

    #region DimensionLabel

    private string _dimensionLabel = "";
    public string DimensionLabel
    {
        get => _dimensionLabel;
        private set
        {
            if (!SetField(ref _dimensionLabel, value)) return;
            this.RaisePropertyChanged(nameof(ShowDimensionLabel));
        }
    }

    #endregion

    public bool ShowDimensionLabel => !string.IsNullOrWhiteSpace(DimensionLabel);
    public bool ShowStatusText => !IsLoading && !HasPreviewImage && !string.IsNullOrWhiteSpace(StatusText);

    #region PreviewImage

    private IImage? _previewImage;
    public IImage? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (!SetField(ref _previewImage, value)) return;
            this.RaisePropertyChanged(nameof(HasPreviewImage));
            this.RaisePropertyChanged(nameof(ShowStatusText));
        }
    }

    #endregion

    public bool HasPreviewImage => PreviewImage != null;

    #region IsLoading

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetField(ref _isLoading, value)) return;
            this.RaisePropertyChanged(nameof(ShowStatusText));
        }
    }

    #endregion

    #region StatusText

    private string _statusText = "Loading texture preview...";
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (!SetField(ref _statusText, value)) return;
            this.RaisePropertyChanged(nameof(ShowStatusText));
        }
    }

    #endregion

    private readonly string _assetPath;
    private Bitmap? _previewBitmap;
    private readonly CancellationTokenSource _loadCTS = new();

#pragma warning disable CS8618
    public TextureMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public TextureMonitorDrawerViewModel(IAssetSelectable assetSelection)
    {
        AssetName = assetSelection.AssetName;
        AssetId = assetSelection.AssetId;
        AssetIdLabel = string.IsNullOrWhiteSpace(AssetId) ? "ID: <missing>" : $"ID: {AssetId}";
        var assetSize = AssetFileInfoUtility.TryGetFileSize(assetSelection.AssetPath);
        AssetSizeLabel = assetSize.HasValue
            ? $"Size: {FileSizeFormatter.FormatBytes(assetSize.Value)}"
            : "Size: <unknown>";
        _assetPath = assetSelection.AssetPath;
        _ = LoadPreviewAsync(_loadCTS.Token);
    }

    public override void Dispose()
    {
        base.Dispose();
        _loadCTS.Cancel();
        _loadCTS.Dispose();
        _previewBitmap?.Dispose();
    }

    private async Task LoadPreviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            var previewBitmap = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = File.OpenRead(_assetPath);
                return new Bitmap(stream);
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                previewBitmap.Dispose();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    previewBitmap.Dispose();
                    return;
                }

                _previewBitmap = previewBitmap;
                PreviewImage = _previewBitmap;
                DimensionLabel = $"{_previewBitmap.PixelSize.Width} x {_previewBitmap.PixelSize.Height}";
                StatusText = "";
                IsLoading = false;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"Failed to load texture preview. {e.Message}";
                IsLoading = false;
            });
        }
    }
}
