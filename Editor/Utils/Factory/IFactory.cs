namespace Editor.Utils.Factory;

public interface IFactory<out T> where T : class
{
	T CreateInstance();
}