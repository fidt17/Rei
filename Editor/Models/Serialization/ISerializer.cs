namespace Editor.Models.Serialization;

public interface ISerializer<T>
{
	string Serialize(T obj);
	T? Deserialize(string obj);
}