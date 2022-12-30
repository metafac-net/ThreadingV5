using System;
using System.Threading.Tasks;

namespace MetaFac.Threading.Core
{
    public interface IQueueWriter<in T> : IDisposable
    {
        ValueTask EnqueueAsync(T item);
        void Complete();

        bool TryEnqueue(T item);
        bool TryComplete();
    }
}