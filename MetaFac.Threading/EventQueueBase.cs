using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public abstract class EventQueueBase<T> : Disposable, IQueueWriter<T>, IQueueReader<T>
    {
        private readonly IQueueWriter<T> _queue;

        protected EventQueueBase(CancellationToken shutdownToken)
        {
            _queue = new ChannelQueue<T>(this, shutdownToken);
        }

        protected override ValueTask OnDisposeAsync()
        {
            _queue.Dispose();
            return new ValueTask();
        }

        public ValueTask EnqueueAsync(T item)
        {
            return _queue.EnqueueAsync(item);
        }

        public void Complete()
        {
            _queue.Complete();
        }
        protected abstract void OnDequeued(T @event);

        public ValueTask OnDequeueAsync(T item)
        {
            OnDequeued(item);
            return new ValueTask();
        }

        public void OnComplete()
        {
        }

    }
}