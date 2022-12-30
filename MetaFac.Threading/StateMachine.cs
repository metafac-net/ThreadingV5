using MetaFac.Threading.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class StateMachine<TState, TEvent> : IQueueWriter<TEvent>, IQueueReader<TEvent>
    {
        private readonly IQueueWriter<TEvent> _queue;
        private readonly IStateEventHandler<TState, TEvent> _handler;
        private TState _currentState;

        public StateMachine(
            TState initialState, 
            IStateEventHandler<TState, TEvent> handler,
            Func<IQueueReader<TEvent>, IQueueWriter<TEvent>> queueFactory)
        {
            _queue = queueFactory(this);
            _currentState = initialState;
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
            _currentState = _handler.HandleEvent(_currentState, item);
            return new ValueTask();
        }

        public void OnComplete()
        {
            // not used
        }
    }
}
