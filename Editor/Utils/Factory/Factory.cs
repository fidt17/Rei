using System;

namespace Editor.Utils.Factory;

public class Factory<T> : IFactory<T> where T : class
{
	private readonly Func<T> _factory;

	public Factory(Func<T> factory)
	{
		_factory = factory;
	}

	public T CreateInstance()
	{
		return _factory();
	}
}
