namespace ReiEditor.Utils.Factory;

public interface IFactory<out T> where T : class
{
	T CreateInstance();
	T CreateInstance(params object[] parameters);
}