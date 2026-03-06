using System.Collections.Generic;

namespace ReiEditor.Models.Services.Assets.Search;

public interface IAssetSearchService
{
    IReadOnlyList<AssetSearchResult> Search(string query);
    IReadOnlyList<AssetSearchResult> SearchByExtensions(string query, IReadOnlyCollection<string> extensions);
}
