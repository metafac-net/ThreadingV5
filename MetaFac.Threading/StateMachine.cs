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
        //public TState Snapshot => _currentState;

        public StateMachine(CancellationToken token, IStateEventHandler<TState, TEvent> handler, TState initialState)
        {
            _queue = new ChannelQueue<TEvent>(this, token);
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _currentState = initialState;
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
            _currentState = _handler.HandleEvent(_currentState, item);
            return new ValueTask();
        }

        public void OnComplete()
        {
            // not used
        }
    }
}
