using System;
using Autofac;

namespace Editor.Utils.Factory;

public class Factory<T> : IFactory<T> where T : class
{
	private readonly Func<T> _factory;

	public Factory(IComponentContext context)
	{
		_factory = context.Resolve<T>;
	}

	public T CreateInstance()
	{
		return _factory();
	}
}
