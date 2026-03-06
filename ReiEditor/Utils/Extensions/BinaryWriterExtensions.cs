using System.IO;

namespace ReiEditor.Utils.Extensions;

public static class BinaryWriterExtensions
{
	public static void WriteString(this BinaryWriter bw, string str)
	{
		bw.Write(str.Length);
		bw.Write(str.ToCharArray(), 0, str.Length);
	}
}