namespace ReiEditor.Models.Resources;

public class ObjectFile<T>
{
    public T Object { get; }
    public string FullPath { get; }

    public ObjectFile(T o, string fullPath)
    {
        Object = o;
        FullPath = fullPath;
    }
}