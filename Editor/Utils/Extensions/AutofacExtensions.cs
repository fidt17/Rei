using Autofac;
using Editor.Utils.Factory;

namespace Editor.Utils.Extensions;

public static class AutofacExtensions
{
	public static void RegisterFactory<T>(this ContainerBuilder builder) where T : class
	{
		builder.RegisterType<T>();
		builder.Register(c =>
		{
			var context = c.Resolve<IComponentContext>();
			return new Factory<T>(context.Resolve<T>);
		}).As<IFactory<T>>().SingleInstance();
	}
}