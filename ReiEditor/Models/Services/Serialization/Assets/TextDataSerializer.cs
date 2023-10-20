using System.IO;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Serialization.Assets;

public class TextDataSerializer : IBinarySerializer<object>
{
	private readonly ISerializer _serializer;

	public TextDataSerializer(ISerializer serializer)
	{
		_serializer = serializer;
	}

	public void Serialize(object target, BinaryWriter writer)
	{
		writer.WriteString(_serializer.Serialize(target));
	}
}