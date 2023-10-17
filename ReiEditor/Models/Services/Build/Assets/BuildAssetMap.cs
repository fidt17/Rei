using System;
using System.Collections.Generic;

namespace ReiEditor.Models.Services.Build.Assets;

public class BuildAssetMap
{
	public struct AssetBuildInfo
	{
		public string Id { get; }
		public string Path { get; }
		public long Offset { get; }

		public AssetBuildInfo(string id, string path, long offset)
		{
			Id = id;
			Path = path;
			Offset = offset;
		}
	}

	public IEnumerable<AssetBuildInfo> Assets => _assets;

	private readonly List<AssetBuildInfo> _assets = new();

	public void Add(AssetBuildInfo a)
	{
		if (_assets.Contains(a) || _assets.Exists(x => x.Id == a.Id)) throw new Exception("Duplicate asset build info");
		_assets.Add(a);
	}
}