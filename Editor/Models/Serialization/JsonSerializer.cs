using Newtonsoft.Json;

namespace Editor.Models.Serialization;

public class JsonSerializer<T> : ISerializer<T>
{
	public string Serialize(T obj)
	{
		var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
		return json;
	}

	public T? Deserialize(string obj)
	{
		var t = JsonConvert.DeserializeObject<T>(obj);
		return t;
	}
}