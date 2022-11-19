using System;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public class ShardPool<TInstance, T> : IDisposable
        where TInstance : IQueueWriter<T>, IDisposable
    {

        private readonly int _maxThreads;
        private readonly TInstance[] _instances;

        public ShardPool(Func<TInstance> factory, int maxThreads = 0)
        {
            _maxThreads = maxThreads > 0 ? maxThreads : Environment.ProcessorCount;
            _instances = new TInstance[_maxThreads];

            for (int t = 0; t < _maxThreads; t++)
            {
                _instances[t] = factory();
            }
        }

        public ValueTask EnqueueAsync(int shard, T value)
        {
            int index = shard % _maxThreads;
            if (index < 0) index = index + _maxThreads;
            return _instances[index].EnqueueAsync(value);
        }

        public void Dispose()
        {
            for (int t = 0; t < _maxThreads; t++)
            {
                _instances[t].Dispose();
            }
        }

    }
}