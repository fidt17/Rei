using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Core;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Utils.Factory;

public class Factory<T> : IFactory<T> where T : class
{
	private readonly IComponentContext _context;
	private readonly ILogger<Factory<T>> _logger;

	public Factory(IComponentContext context, ILogger<Factory<T>> logger)
	{
		_context = context;
		_logger = logger;
	}

	public T CreateInstance()
	{
		try
		{
			return _context.Resolve<T>();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			throw;
		}
	}

	public T CreateInstance(params object[] parameters)
	{
		try
		{
			var p = new List<Parameter>();
			foreach (var parameter in parameters)
			{
				p.Add(new TypedParameter(parameter.GetType(), parameter));
			}

			return _context.Resolve<T>(p);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			throw;
		}
	}
}