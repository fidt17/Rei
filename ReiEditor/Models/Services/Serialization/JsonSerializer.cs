using System;
using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Serialization;

public class JsonSerializer : ISerializer
{
	public string Serialize<T>(T obj)
	{
		var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
		return json;
	}

	public T Deserialize<T>(string source)
	{
		var t = JsonConvert.DeserializeObject<T>(source);
		if (t == null) throw new Exception($"Could not deserialize [{source}] to [{typeof(T)}]");

		return t;
	}

	public T Deserialize<T>(string source, T defaultValue)
	{
		return Deserialize<T>(source) ?? defaultValue;
	}
}