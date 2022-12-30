using System;
using System.Threading.Tasks;

namespace MetaFac.Threading.Core
{
    public interface IQueueReader<in T> : IDisposable
    {
        ValueTask OnDequeueAsync(T item);
        void OnComplete();
    }
}