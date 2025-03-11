using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class StateMachineTests
    {
        private class NullHandler<TState, TEvent> : IStateEventHandler<TState, TEvent>
        {
            private readonly TaskCompletionSource<TState> _tcs = new TaskCompletionSource<TState>();
            public Task<TState> Task => _tcs.Task;

            private readonly Func<TEvent, bool> _completionFn;

            public NullHandler(Func<TEvent, bool> completionFn)
            {
                _completionFn = completionFn ?? throw new ArgumentNullException(nameof(completionFn));
            }
            public TState HandleEvent(TState state, TEvent @event)
            {
                if (_completionFn(@event))
                    _tcs.TrySetResult(state);

                return state;
            }
            public TState CancelEvent(TState state, TEvent @event)
            {
                _tcs.TrySetCanceled();
                return state;
            }
        }

        private sealed class ImmutableStatistics
        {
            private static readonly ImmutableStatistics _empty = new ImmutableStatistics();
            public static ImmutableStatistics Empty => _empty;

            public readonly int N = 0;
            public readonly long S1 = 0;
            public readonly long S2 = 0;

            public ImmutableStatistics() { }

            private ImmutableStatistics(int n, long s1, long s2)
            {
                N = n;
                S1 = s1;
                S2 = s2;
            }

            public ImmutableStatistics AddSample(int value)
            {
                return new ImmutableStatistics(N + 1, S1 + value, S2 + (value * value));
            }
        }

        private sealed class MutableStatistics
        {
            public int N { get; private set; }
            public long S1 { get; private set; }
            public long S2 { get; private set; }

            public void AddSample(int value)
            {
                N += 1;
                S1 += value;
                S2 += (value * value);
            }
        }

        private sealed class Sample
        {
            public readonly int Value;

            public Sample(int value)
            {
                Value = value;
            }
        }

        private sealed class ImmutableStatsHandler : IStateEventHandler<ImmutableStatistics, Sample>
        {
            private readonly TaskCompletionSource<ImmutableStatistics> _tcs = new TaskCompletionSource<ImmutableStatistics>();
            public Task<ImmutableStatistics> Complete => _tcs.Task;

            private readonly Func<Sample, bool> _completionFn;

            public ImmutableStatsHandler(Func<Sample, bool> completionFn)
            {
                _completionFn = completionFn ?? throw new ArgumentNullException(nameof(completionFn));
            }

            public ImmutableStatistics HandleEvent(ImmutableStatistics state, Sample @event)
            {
                var newState = (state ?? new ImmutableStatistics()).AddSample(@event.Value);
                if (_completionFn(@event))
                    _tcs.TrySetResult(newState);
                return newState;
            }
            public ImmutableStatistics CancelEvent(ImmutableStatistics state, Sample @event)
            {
                _tcs.TrySetCanceled();
                return state;
            }

        }

        private sealed class MutableStatsHandler : IStateEventHandler<MutableStatistics, Sample>
        {
            private readonly TaskCompletionSource<MutableStatistics> _tcs = new TaskCompletionSource<MutableStatistics>();
            public Task<MutableStatistics> Complete => _tcs.Task;

            private readonly Func<Sample, bool> _completionFn;

            public MutableStatsHandler(Func<Sample, bool> completionFn)
            {
                _completionFn = completionFn ?? throw new ArgumentNullException(nameof(completionFn));
            }

            public MutableStatistics HandleEvent(MutableStatistics state, Sample @event)
            {
                if (state is null) state = new MutableStatistics();
                state.AddSample(@event.Value);
                if (_completionFn(@event))
                    _tcs.TrySetResult(state);
                return state;
            }

            public MutableStatistics CancelEvent(MutableStatistics state, Sample @event)
            {
                _tcs.TrySetCanceled();
                return state;
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task TypesAreValueType(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<int>();
            var machine = new NullHandler<long, int>((i) => i == 0);
            using (var queue = new StateMachine<long, int>(default, machine, queueFactory))
            {

                await queue.EnqueueAsync(1);
                await queue.EnqueueAsync(0);

                await machine.Task;
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task TypesAreRefType(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<string>();
            var machine = new NullHandler<string, string>((s) => string.IsNullOrEmpty(s));
            using (var queue = new StateMachine<string, string>(string.Empty, machine, queueFactory))
            {
                await queue.EnqueueAsync("1");
                await queue.EnqueueAsync("");
                await machine.Task;
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task TypesAreEnumType(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<string>();
            var machine = new NullHandler<DayOfWeek, string>((s) => string.IsNullOrEmpty(s));
            using (var queue = new StateMachine<DayOfWeek, string>(default, machine, queueFactory))
            {
                await queue.EnqueueAsync("1");
                await queue.EnqueueAsync("");
                var state = await machine.Task;
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task EnqueueEvents_ImmutableState(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<Sample>();
            var handler = new ImmutableStatsHandler((s) => s.Value == 0);
            using (var queue = new StateMachine<ImmutableStatistics, Sample>(ImmutableStatistics.Empty, handler, queueFactory))
            {
                for (int i = 0; i < 5; i++)
                {
                    await queue.EnqueueAsync(new Sample((i + 1) * 2));
                }
                await queue.EnqueueAsync(new Sample(0));
                var snapshot = await handler.Complete;

                snapshot.N.ShouldBe(6);
                snapshot.S1.ShouldBe(30L);
                snapshot.S2.ShouldBe(220L);
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task EnqueueEvents_MutableState(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<Sample>();
            var handler = new MutableStatsHandler((s) => s.Value == 0);
            using (var queue = new StateMachine<MutableStatistics, Sample>(new MutableStatistics(), handler, queueFactory))
            {
                for (int i = 0; i < 5; i++)
                {
                    await queue.EnqueueAsync(new Sample((i + 1) * 2));
                }
                await queue.EnqueueAsync(new Sample(0));
                var snapshot = await handler.Complete;

                snapshot.N.ShouldBe(6);
                snapshot.S1.ShouldBe(30L);
                snapshot.S2.ShouldBe(220L);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(100)]
        [InlineData(1_000)]
        [InlineData(10_000)]
        [InlineData(100_000)]
        public async Task EnqueueManyEvents(int iterations)
        {
            QueueImpl impl = QueueImpl.UnboundedChannelQueue;
            var queueFactory = impl.GetFactory<Sample>();
            var handler = new ImmutableStatsHandler((s) => s.Value == 0);
            using (var queue = new StateMachine<ImmutableStatistics, Sample>(ImmutableStatistics.Empty, handler, queueFactory))
            {
                for (int i = 1; i < iterations; i++)
                {
                    await queue.EnqueueAsync(new Sample(i));
                }
                await queue.EnqueueAsync(new Sample(0));
                var snapshot = await handler.Complete;
                snapshot.N.ShouldBe(iterations);
            }
        }
    }
}