namespace ReiEditor.Models.Services.Serialization;

public interface ISerializer
{
	string Serialize<T>(T obj);
	T Deserialize<T>(string source);
	T Deserialize<T>(string source, T defaultValue);
}