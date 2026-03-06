using Autofac;
using Autofac.Builder;

namespace ReiEditor.Utils.Extensions;

public static class AutofacExtensions
{
	public static IRegistrationBuilder<TImplementer, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterSingleton<TImplementer>(this ContainerBuilder builder) where TImplementer : notnull
	{
		return builder.RegisterType<TImplementer>().SingleInstance();
	}

	public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterNonLazy<T>(this ContainerBuilder builder) where T : class
	{
		builder.RegisterBuildCallback(scope => scope.Resolve<T>());
		return builder.RegisterType<T>();
	}
}