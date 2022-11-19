using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class EventProcessor<TEvent> : IQueueWriter<TEvent>, IQueueReader<TEvent>
    {
        private readonly CancellationToken _shutdownToken;
        private readonly IQueueWriter<TEvent> _queue;
        private readonly IEventHandler<TEvent> _handler;

        public EventProcessor(CancellationToken shutdownToken, IEventHandler<TEvent> handler)
        {
            _shutdownToken = shutdownToken;
            _queue = new ChannelQueue<TEvent>(this, _shutdownToken);
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Dispose()
        {
            _queue.Dispose();
        }

        public ValueTask EnqueueAsync(TEvent item)
        {
            return _queue.EnqueueAsync(item);
        }

        public void Complete()
        {
            _queue.Complete();
        }

        public ValueTask OnDequeueAsync(TEvent item)
        {
            if (_shutdownToken.IsCancellationRequested)
            {
                _handler.CancelEvent(item);
            }
            else
            {
                _handler.HandleEvent(item);
            }
            return new ValueTask();
        }

        public void OnComplete()
        {
            // not used
        }
    }
}
