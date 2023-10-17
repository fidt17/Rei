using System.IO;

namespace ReiEditor.Models.Services.Serialization;

public interface IBinarySerializer
{
	void Serialize<T>(T target, BinaryWriter writer);
}

public interface IBinarySerializer<in T>
{
	void Serialize(T target, BinaryWriter writer);
}