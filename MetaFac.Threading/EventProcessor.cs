using MetaFac.Threading.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class EventProcessor<TEvent> : IQueueWriter<TEvent>, IQueueReader<TEvent>
    {
        private readonly IQueueWriter<TEvent> _queue;
        private readonly IEventHandler<TEvent> _handler;

        public EventProcessor(
            IEventHandler<TEvent> handler,
            Func<IQueueReader<TEvent>, IQueueWriter<TEvent>> queueFactory)
        {
            _queue = queueFactory(this);
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Dispose()
        {
            _queue.Dispose();
        }

        public bool TryEnqueue(TEvent item)
        {
            return _queue.TryEnqueue(item);
        }

        public ValueTask EnqueueAsync(TEvent item)
        {
            return _queue.EnqueueAsync(item);
        }

        public void Complete()
        {
            _queue.Complete();
        }

        public bool TryComplete()
        {
            return _queue.TryComplete();
        }

        public ValueTask OnDequeueAsync(TEvent item)
        {
            _handler.HandleEvent(item);
            return new ValueTask();
        }

        public void OnComplete()
        {
            // not used
        }
    }
}
