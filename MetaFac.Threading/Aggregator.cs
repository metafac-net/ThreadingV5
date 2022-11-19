using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class Aggregator<TState, TEvent> : IQueueWriter<TEvent>, IQueueReader<TEvent>
    {
        private readonly IQueueWriter<TEvent> _queue;
        private readonly TaskCompletionSource<TState> _tcs = new TaskCompletionSource<TState>();
        private readonly Func<TState, TEvent, TState> _eventHandler;

        // managed state
        private TState _state;
        private bool _completed = false;
        public TState DirtyState => _state;
        public Task<TState> FinalState => _tcs.Task;

        public Aggregator(CancellationToken token, TState initialState, Func<TState, TEvent, TState> eventHandler)
        {
            _queue = new ChannelQueue<TEvent>(this, token);
            _state = initialState;
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
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
            _state = _eventHandler(_state, item);
            return new ValueTask();
        }

        public void OnComplete()
        {
            if (_completed) return;
            _completed = true;
            _tcs.TrySetResult(_state);
        }

    }
}