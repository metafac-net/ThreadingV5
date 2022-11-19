using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading.Benchmarks
{

    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net60)]
    //[SimpleJob(RuntimeMoniker.Net70)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class EventQueues
    {
        private const int EventCount = 1_000_000;

        [Params(false, true)]
        public bool WithCancel;

        [GlobalSetup]
        public void Setup()
        {
        }

        private sealed class State
        {
            private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
            public Task<bool> Task { get { return _tcs.Task; } }
            public void SetDone()
            {
                _tcs.TrySetResult(true);
            }
        }
        private readonly struct Event
        {
            public readonly bool Done;

            private Event(bool done) : this()
            {
                Done = done;
            }

            public static Event True { get; } = new Event(true);
            public static Event False { get; } = new Event(false);
        }
        private sealed class StateEventHandler : IStateEventHandler<State, Event>
        {
            public State CancelEvent(State state, Event @event)
            {
                return state;
            }

            public State HandleEvent(State state, Event @event)
            {
                if (state is null) state = new State();
                if (@event.Done)
                {
                    state.SetDone();
                }
                return state;
            }
        }

        private sealed class EventHandler : IEventHandler<Event>
        {
            private readonly State state = new State();
            public State State => state;
            public void CancelEvent(Event @event)
            {
            }

            public void HandleEvent(Event @event)
            {
                if (@event.Done)
                {
                    state.SetDone();
                }
            }
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = EventCount)]
        public async Task EventProcessor()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            var handler = new EventHandler();
            using var queue = new EventProcessor<Event>(token, handler);

            for (int i = 0; i < EventCount; i++)
            {
                await queue.EnqueueAsync(Event.False).ConfigureAwait(false);
            }

            await queue.EnqueueAsync(Event.True).ConfigureAwait(false);
            bool result = await handler.State.Task.ConfigureAwait(false);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task StateMachine()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            var state = new State();
            using var queue = new StateMachine<State, Event>(token, new StateEventHandler(), state);

            for (int i = 0; i < EventCount; i++)
            {
                await queue.EnqueueAsync(Event.False).ConfigureAwait(false);
            }

            await queue.EnqueueAsync(Event.True).ConfigureAwait(false);
            bool result = await state.Task.ConfigureAwait(false);
        }

        private static int DoWork(int input, CancellationToken token)
        {
            return input * input;
        }

        private static ValueTask<int> DoWorkAsync(int input, CancellationToken token)
        {
            return new ValueTask<int>(input * input);
        }

        private static Task<int> DoWorkTask(int input, CancellationToken token)
        {
            return Task.FromResult<int>(input * input);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task Aggregator()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            var aggregator = new Aggregator<long, int>(token, 0L, (s, e) => s + e);

            Parallel.For(1, EventCount, async (i) =>
            {
                await aggregator.EnqueueAsync(i).ConfigureAwait(false);
            });

            await aggregator.EnqueueAsync(0).ConfigureAwait(false);
            aggregator.Complete();

            long result = await aggregator.FinalState.ConfigureAwait(false);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task Sequencer_L1()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            var sequencer = new Sequencer(token);

            Parallel.For(0, EventCount, (i) =>
            {
                int[] sequencerKeys = new int[] { i };
                var item = new ExecutableItem<int, int>(i, token, DoWork);
                sequencer
                    .SequenceWorkItemAsync(sequencerKeys, item)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            });

            var lastItem = new ExecutableItem<int, int>(EventCount, token, DoWork);
            await sequencer.SequenceWorkItemAsync(new int[0], lastItem).ConfigureAwait(false);

            int result = await lastItem.GetTask().ConfigureAwait(false);
        }

    }
}