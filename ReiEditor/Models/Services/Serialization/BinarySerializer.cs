using System.IO;
using Autofac;

namespace ReiEditor.Models.Services.Serialization;

public class BinarySerializer : IBinarySerializer
{
	private readonly IComponentContext _context;

	public BinarySerializer(IComponentContext context)
	{
		_context = context;
	}

	public void Serialize<T>(T target, BinaryWriter writer)
	{
		_context.Resolve<IBinarySerializer<T>>().Serialize(target, writer);
	}
}