using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public interface IQueueReader<in T> : IDisposable
    {
        ValueTask OnDequeueAsync(T item);
        void OnComplete();
    }
}