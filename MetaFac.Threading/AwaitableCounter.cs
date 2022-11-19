using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public class AwaitableCounter
    {
        private readonly AwaitableCounter? _parent = null;

        private class InternalState
        {
            public long Counter;
            public bool Complete;
            public bool Result;
            public TaskCompletionSource<bool>? Signal;
            public InternalState(long counter) => Counter = counter;
        }

        private InternalState? _state;

        public AwaitableCounter(AwaitableCounter? parent = null)
        {
            _parent = parent;
            _state = new InternalState(0L);
            _state.Complete = true;
            _state.Result = true;
        }

        public AwaitableCounter(long initialCount, AwaitableCounter? parent = null)
        {
            _parent = parent;
            _state = new InternalState(initialCount);
            _state.Complete = false;
            _state.Result = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InternalState AcquireInterlockedState()
        {
            InternalState? state;
            do
            {
                state = Interlocked.Exchange(ref _state, null);
            }
            while (state is null);
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReleaseInterlockedState(InternalState state)
        {
            Interlocked.Exchange(ref _state, state);
        }

        public long Count
        {
            get
            {
                InternalState state = AcquireInterlockedState();
                try
                {
                    return state.Counter;
                }
                finally
                {
                    ReleaseInterlockedState(state);
                }
            }
        }

        public (long count, bool complete) State
        {
            get
            {
                InternalState state = AcquireInterlockedState();
                try
                {
                    return (state.Counter, state.Complete);
                }
                finally
                {
                    ReleaseInterlockedState(state);
                }
            }
        }

        public Task<bool> UntilZero
        {
            get
            {
                InternalState state = AcquireInterlockedState();
                while (state is null);
                try
                {
                    if (state.Signal is null)
                    {
                        state.Signal = new TaskCompletionSource<bool>();
                        if (state.Complete)
                        {
                            state.Signal.SetResult(state.Result);
                        }
                    }
                    return state.Signal.Task;
                }
                finally
                {
                    ReleaseInterlockedState(state);
                }
            }
        }

        public void Increment(bool recursive = false)
        {
            if (recursive) _parent?.Increment(recursive);
            InternalState state = AcquireInterlockedState();
            try
            {
                long counter = ++state.Counter;
                if (counter == 0)
                {
                    state.Complete = true;
                    state.Result = false;
                    if (!(state.Signal is null))
                    {
                        state.Signal.SetResult(false);
                    }
                }
                if (counter == 1)
                {
                    state.Complete = false;
                    state.Signal = null;
                }

            }
            finally
            {
                ReleaseInterlockedState(state);
            }
        }

        public void Decrement(bool recursive = false)
        {
            InternalState state = AcquireInterlockedState();
            try
            {
                long counter = --state.Counter;
                if (counter == 0)
                {
                    state.Complete = true;
                    state.Result = true;
                    if (!(state.Signal is null))
                    {
                        state.Signal.SetResult(true);
                    }
                }
                if (counter == -1)
                {
                    state.Complete = false;
                    state.Signal = null;
                }
            }
            finally
            {
                ReleaseInterlockedState(state);
            }
            if (recursive) _parent?.Decrement(recursive);
        }

    }
}