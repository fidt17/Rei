using ReiEditor.Utils;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Controls;

public sealed class SearchFieldViewModel : BaseViewModel
{
    public ObservableField<string> Query { get; } = new("");
    public ObservableField<bool> HasQuery { get; } = new(false);
    public ObservableField<bool> ShowPlaceholder { get; } = new(true);
    public RelayCommand ClearCommand { get; }

    private bool _isFocused;
    private bool _suppressQueryRefresh;

    public SearchFieldViewModel()
    {
        ClearCommand = new RelayCommand(Clear);
        Query.ChangedEvent += HandleQueryChanged;
    }

    public override void Dispose()
    {
        base.Dispose();
        Query.ChangedEvent -= HandleQueryChanged;
    }

    public void SetFocused(bool isFocused)
    {
        _isFocused = isFocused;
        UpdatePlaceholderVisibility();
    }

    public void ResetSearch()
    {
        SetQuery("", suppressRefresh: true);
    }

    public bool ShouldSuppressQueryRefresh() => _suppressQueryRefresh;

    private void HandleQueryChanged(string query)
    {
        HasQuery.Value = !string.IsNullOrWhiteSpace(query);
        UpdatePlaceholderVisibility();
    }

    private void UpdatePlaceholderVisibility()
    {
        ShowPlaceholder.Value = !HasQuery.Value && !_isFocused;
    }

    private void Clear()
    {
        SetQuery("", suppressRefresh: false);
    }

    private void SetQuery(string query, bool suppressRefresh)
    {
        _suppressQueryRefresh = suppressRefresh;
        Query.Value = query;
        _suppressQueryRefresh = false;
    }
}
