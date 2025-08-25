using System;
using System.Collections.Concurrent;

namespace ReiEditor.Utils.Common;

public class Pool<T>
{
    private readonly Func<T> factoryFunction;
    private readonly ConcurrentQueue<T> _pool = new();

    public Pool(Func<T> factoryFunction)
    {
        this.factoryFunction = factoryFunction;
    }

    public T Get()
    {
        if (_pool.IsEmpty)
        {
            Populate(1);
        }

        _pool.TryDequeue(out T? value);

        return value ?? throw new Exception($"Failed at retrieving an object from {nameof(Pool<T>)}");
    }

    public void Put(T value)
    {
        _pool.Enqueue(value);
    }

    public void Populate(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Put(factoryFunction());
        }
    }
}