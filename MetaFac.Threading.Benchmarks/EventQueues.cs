using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using MetaFac.Threading.Channels;
using MetaFac.Threading.Core;
using MetaFac.Threading.Disruptor;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading.Benchmarks
{
    public enum QueueImpl
    {
        UnboundedChannel,
        BoundedChannel1K,
        Disruptor1K,
    }

    internal static class QueueImplementationHelper
    {
        public static Func<IQueueReader<T>, IQueueWriter<T>> GetFactory<T>(this QueueImpl impl)
        {
            switch (impl)
            {
                case QueueImpl.UnboundedChannel:
                    return (reader) => new UnboundedChannelQueue<T>(reader);
                case QueueImpl.BoundedChannel1K:
                    return (reader) => new BoundedChannelQueue<T>(reader, 1024);
                case QueueImpl.Disruptor1K:
                    return (reader) => new DisruptorQueue<T>(reader, 1024);
                default:
                    throw new ArgumentOutOfRangeException(nameof(impl), impl, null);
            }
        }
    }


    [MemoryDiagnoser]
    //[SimpleJob(RuntimeMoniker.Net60)]
    [SimpleJob(RuntimeMoniker.Net70)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class EventQueues
    {
        private const int EventCount = 1_000_000;

        [Params(QueueImpl.UnboundedChannel, QueueImpl.BoundedChannel1K, QueueImpl.Disruptor1K)]
        public QueueImpl QueueType;

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
            var handler = new EventHandler();
            var queueFactory = QueueType.GetFactory<Event>();
            using var queue = new EventProcessor<Event>(handler, queueFactory);

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
            var state = new State();
            var queueFactory = QueueType.GetFactory<Event>();
            using var queue = new StateMachine<State, Event>(state, new StateEventHandler(), queueFactory);

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
            var queueFactory = QueueType.GetFactory<int>();
            var aggregator = new Aggregator<long, int>(0L, (s, e) => s + e, queueFactory);

            Parallel.For(1, EventCount, async (i) =>
            {
                await aggregator.EnqueueAsync(i).ConfigureAwait(false);
            });

            await aggregator.EnqueueAsync(0).ConfigureAwait(false);
            aggregator.Complete();

            long result = await aggregator.FinalState.ConfigureAwait(false);
        }

    }
}