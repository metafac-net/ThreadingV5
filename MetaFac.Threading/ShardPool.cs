using MetaFac.Threading.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public class ShardPool<TEvent> : IDisposable
    {

        private readonly int _maxThreads;
        private readonly IQueueWriter<TEvent>[] _instances;

        public ShardPool(
            Func<IQueueReader<TEvent>, IQueueWriter<TEvent>> factory, 
            IQueueReader<TEvent> reader,
            int maxThreads = 0)
        {
            _maxThreads = maxThreads > 0 ? maxThreads : Environment.ProcessorCount;
            _instances = new IQueueWriter<TEvent>[_maxThreads];

            for (int t = 0; t < _maxThreads; t++)
            {
                _instances[t] = factory(reader);
            }
        }

        public ValueTask EnqueueAsync(int shard, TEvent value)
        {
            int index = shard % _maxThreads;
            if (index < 0) index = index + _maxThreads;
            return _instances[index].EnqueueAsync(value);
        }

        public bool TryEnqueue(int shard, TEvent value)
        {
            int index = shard % _maxThreads;
            if (index < 0) index = index + _maxThreads;
            return _instances[index].TryEnqueue(value);
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