using System.Collections.Generic;

namespace ReiEditor.Models.Services.Assets.Search;

public interface IAssetSearchService
{
    IReadOnlyList<AssetSearchResult> Search(string query);
}
