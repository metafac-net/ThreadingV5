using System;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public class StatefulObserver<TState, TEvent> : IObserver<TEvent>
    {
        private readonly TaskCompletionSource<TState> _tcs = new TaskCompletionSource<TState>();
        private readonly Func<TState, TEvent, TState> _eventHandler;

        // managed state
        private TState _state;
        private bool _completed = false;
        public TState DirtyState => _state;
        public Task<TState> FinalState => _tcs.Task;

        public StatefulObserver(TState initialState, Func<TState, TEvent, TState> eventHandler)
        {
            _state = initialState;
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
        }


        public void OnCompleted()
        {
            if (_completed) return;
            _completed = true;
            _tcs.TrySetResult(_state);
        }

        public void OnError(Exception e)
        {
            if (_completed) return;
            _completed = true;
            _tcs.TrySetException(e);
        }

        public void OnNext(TEvent value)
        {
            if (_completed) return;
            _state = _eventHandler(_state, value);
        }
    }
}
