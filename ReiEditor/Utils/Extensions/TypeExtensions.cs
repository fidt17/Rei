using System;
using System.Collections.Generic;
using System.Linq;

namespace ReiEditor.Utils.Extensions;

public static class TypeExtensions
{
	private static readonly HashSet<Type> _integerTypes = new()
	{
		typeof(int), typeof(long), typeof(short), typeof(sbyte), typeof(byte), typeof(ulong), typeof(ushort), typeof(uint)
	};

	public static string ExpandTypeName(this Type t)
	{
		return !t.IsGenericType || t.IsGenericTypeDefinition
			? !t.IsGenericTypeDefinition ? t.Name : t.Name.Remove(t.Name.IndexOf('`'))
			: $"{ExpandTypeName(t.GetGenericTypeDefinition())}<{string.Join(',', t.GetGenericArguments().Select(ExpandTypeName))}>";
	}

	public static bool IsInteger(this Type t) => _integerTypes.Contains(t);
}