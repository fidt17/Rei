using System;
using System.Linq;

namespace ReiEditor.Utils.Extensions;

public static class TypeExtensions
{
	public static string ExpandTypeName(this Type t)
	{
		return !t.IsGenericType || t.IsGenericTypeDefinition
			? !t.IsGenericTypeDefinition ? t.Name : t.Name.Remove(t.Name.IndexOf('`'))
			: $"{ExpandTypeName(t.GetGenericTypeDefinition())}<{string.Join(',', t.GetGenericArguments().Select(ExpandTypeName))}>";
	}
}