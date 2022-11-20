using FluentAssertions;
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

        [Fact]
        public async Task TypesAreValueType()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var machine = new NullHandler<long, int>((i) => i == 0);
            using (var queue = new StateMachine<long, int>(cts.Token, machine, default))
            {

                await queue.EnqueueAsync(1);
                await queue.EnqueueAsync(0);

                await machine.Task;
            }
        }

        [Fact]
        public async Task TypesAreRefType()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var machine = new NullHandler<string, string>((s) => string.IsNullOrEmpty(s));
            using (var queue = new StateMachine<string, string>(cts.Token, machine, string.Empty))
            {

                await queue.EnqueueAsync("1");
                await queue.EnqueueAsync("");

                await machine.Task;
            }
        }

        [Fact]
        public async Task TypesAreEnumType()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var machine = new NullHandler<DayOfWeek, string>((s) => string.IsNullOrEmpty(s));
            using (var queue = new StateMachine<DayOfWeek, string>(cts.Token, machine, default))
            {

                await queue.EnqueueAsync("1");
                await queue.EnqueueAsync("");

                var state = await machine.Task;
            }
        }

        [Fact]
        public async Task EnqueueEvents_ImmutableState()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var handler = new ImmutableStatsHandler((s) => s.Value == 0);
            using (var queue = new StateMachine<ImmutableStatistics, Sample>(cts.Token, handler, ImmutableStatistics.Empty))
            {

                for (int i = 0; i < 5; i++)
                {
                    await queue.EnqueueAsync(new Sample((i + 1) * 2));
                }
                await queue.EnqueueAsync(new Sample(0));
                var snapshot = await handler.Complete;

                snapshot.N.Should().Be(6);
                snapshot.S1.Should().Be(30L);
                snapshot.S2.Should().Be(220L);
            }
        }

        [Fact]
        public async Task EnqueueEvents_MutableState()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var handler = new MutableStatsHandler((s) => s.Value == 0);
            using (var queue = new StateMachine<MutableStatistics, Sample>(cts.Token, handler, new MutableStatistics()))
            {

                for (int i = 0; i < 5; i++)
                {
                    await queue.EnqueueAsync(new Sample((i + 1) * 2));
                }
                await queue.EnqueueAsync(new Sample(0));
                var snapshot = await handler.Complete;

                snapshot.N.Should().Be(6);
                snapshot.S1.Should().Be(30L);
                snapshot.S2.Should().Be(220L);
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var handler = new ImmutableStatsHandler((s) => s.Value == 0);
            using (var queue = new StateMachine<ImmutableStatistics, Sample>(cts.Token, handler, ImmutableStatistics.Empty))
            {

                for (int i = 1; i < iterations; i++)
                {
                    await queue.EnqueueAsync(new Sample(i));
                }
                await queue.EnqueueAsync(new Sample(0));
                var snapshot = await handler.Complete;
                snapshot.N.Should().Be(iterations);
            }
        }
    }
}