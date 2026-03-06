using System;
using System.IO;
using System.Threading.Tasks;

namespace ReiEditor.Models.Resources;

public static class ResourceUtils
{
	public static async Task<string> Load(string resourcePath)
	{
		if (!File.Exists(resourcePath)) throw new Exception($"Resource does not exist at path: {resourcePath}");
		return await File.ReadAllTextAsync(resourcePath); 
	}
}