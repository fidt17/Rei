using System;
using System.Collections.Generic;
using System.Xml;

namespace ReiEditor.Utils.Extensions;

public static class StringExtensions
{
    public static List<int> AllIndexesOf(this string str, string value) 
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("the string to find may not be empty", "value");
        }
        
        var indexes = new List<int>();
        for (int index = 0;; index += value.Length) 
        {
            index = str.IndexOf(value, index, StringComparison.Ordinal);
            
            if (index == -1)
            {
                return indexes;
            }
            
            indexes.Add(index);
        }
    }

    public static string? GetValueFromXml(string xmlInput, string target)
    {
        var xml = new XmlDocument();
        xml.LoadXml(xmlInput);

        var elements = xml.GetElementsByTagName(target);
        return elements.Count == 0 ? null : elements[0]!.InnerText;
    }
}