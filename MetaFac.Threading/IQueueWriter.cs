
using System;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public interface IQueueWriter<in T> : IDisposable
    {
        ValueTask EnqueueAsync(T item);
        void Complete();
    }
}