using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class EventProcessorTests
    {
        private class NullHandler<T> : IEventHandler<T>
        {
            private readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>();
            public Task<T> Task => _tcs.Task;

            private readonly Func<T, bool> _completionFn;

            public NullHandler(Func<T, bool> completionFn)
            {
                _completionFn = completionFn ?? throw new ArgumentNullException(nameof(completionFn));
            }

            public void HandleEvent(T @event)
            {
                if (_completionFn(@event))
                    _tcs.TrySetResult(@event);
            }
            public void CancelEvent(T @event)
            {
                _tcs.TrySetCanceled();
            }
        }

        private sealed class ImmutableStatistics
        {
            public readonly int N = 0;
            public readonly long S1 = 0;
            public readonly long S2 = 0;

            private ImmutableStatistics(int n, long s1, long s2)
            {
                N = n;
                S1 = s1;
                S2 = s2;
            }

            public ImmutableStatistics() { }

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
            public readonly bool IsLast;

            public Sample(int value, bool isLast)
            {
                Value = value;
                IsLast = isLast;
            }
        }

        private sealed class ImmutableStatsHandler : IEventHandler<Sample>
        {
            private readonly TaskCompletionSource<Sample> _tcs = new TaskCompletionSource<Sample>();
            public Task<Sample> Task => _tcs.Task;

            private ImmutableStatistics state = new ImmutableStatistics();
            public ImmutableStatistics State => state;
            public void HandleEvent(Sample @event)
            {
                state = state.AddSample(@event.Value);
                if (@event.IsLast)
                    _tcs.TrySetResult(@event);
            }

            public void CancelEvent(Sample @event)
            {
                _tcs.TrySetCanceled();
            }
        }
        private sealed class MutableStatsHandler : IEventHandler<Sample>
        {
            private readonly TaskCompletionSource<Sample> _tcs = new TaskCompletionSource<Sample>();
            public Task<Sample> Task => _tcs.Task;

            private MutableStatistics state = new MutableStatistics();
            public MutableStatistics State => state;

            public void HandleEvent(Sample @event)
            {
                state.AddSample(@event.Value);
                if (@event.IsLast)
                    _tcs.TrySetResult(@event);
            }

            public void CancelEvent(Sample @event)
            {
                _tcs.TrySetCanceled();
            }
        }

        [Fact]
        public async Task EventTypeIsValueType()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var handler = new NullHandler<long>((x) => x == 0L);
            using (var queue = new EventProcessor<long>(cts.Token, handler))
            {

                await queue.EnqueueAsync(1L);
                await queue.EnqueueAsync(0L);

                long result = await handler.Task;
            }
        }

        [Fact]
        public async Task EventTypeIsRefType()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var handler = new NullHandler<string>((x) => x == string.Empty);
            using (var queue = new EventProcessor<string>(cts.Token, handler))
            {

                await queue.EnqueueAsync("test");
                await queue.EnqueueAsync(string.Empty);

                var result = await handler.Task;
            }
        }

        [Fact]
        public async Task EventTypeIsEnumType()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var handler = new NullHandler<DayOfWeek>((x) => x == DayOfWeek.Sunday);
            using (var queue = new EventProcessor<DayOfWeek>(cts.Token, handler))
            {

                await queue.EnqueueAsync(DayOfWeek.Monday);
                await queue.EnqueueAsync(DayOfWeek.Sunday);

                var result = await handler.Task;
            }
        }

        [Fact]
        public async Task EnqueueEvents_ImmutableState()
        {
            var handler = new ImmutableStatsHandler();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var queue = new EventProcessor<Sample>(cts.Token, handler))
            {

                for (int i = 0; i < 5; i++)
                {
                    await queue.EnqueueAsync(new Sample((i + 1) * 2, i == 4));
                }

                var last = await handler.Task;

                var snapshot = handler.State;
                snapshot.N.Should().Be(5);
                snapshot.S1.Should().Be(30L);
                snapshot.S2.Should().Be(220L);
            }
        }

        [Fact]
        public async Task EnqueueEvents_MutableState()
        {
            var handler = new MutableStatsHandler();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var queue = new EventProcessor<Sample>(cts.Token, handler))
            {

                for (int i = 0; i < 5; i++)
                {
                    await queue.EnqueueAsync(new Sample((i + 1) * 2, i == 4));
                }

                var result = await handler.Task;

                var snapshot = handler.State;
                snapshot.N.Should().Be(5);
                snapshot.S1.Should().Be(30L);
                snapshot.S2.Should().Be(220L);
            }
        }

        [Theory]
        [InlineData(1_000_000)]
        public async Task EnqueueManyEvents(int iterations)
        {
            var handler = new ImmutableStatsHandler();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var queue = new EventProcessor<Sample>(cts.Token, handler))
            {

                for (int i = 0; i < iterations; i++)
                {
                    await queue.EnqueueAsync(new Sample(i, i == (iterations - 1)));
                }

                var result = await handler.Task;

                var snapshot = handler.State;
                snapshot.N.Should().Be(iterations);
            }
        }

    }
}