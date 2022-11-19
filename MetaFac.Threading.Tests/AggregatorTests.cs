using FluentAssertions;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class AggregatorTests
    {
        private readonly struct Stats
        {
            public readonly int Count;
            public readonly long Total;
            public readonly long SumSq;

            public Stats(int count, long total, long sumSq)
            {
                Count = count;
                Total = total;
                SumSq = sumSq;
            }

            public Stats(Stats stats, int value)
            {
                Count = stats.Count + 1;
                Total = stats.Total + value;
                SumSq = stats.SumSq + (value * value);
            }
        }

        [Fact]
        public async Task Aggregator()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var fsm = new Aggregator<Stats, int>(cts.Token, default,
                (s, value) => new Stats(s, value));
            await fsm.EnqueueAsync(4);
            await fsm.EnqueueAsync(3);
            await fsm.EnqueueAsync(2);
            await fsm.EnqueueAsync(1);
            await fsm.EnqueueAsync(0);
            fsm.Complete();
            var result = await fsm.FinalState;
            result.Count.Should().Be(5);
            result.Total.Should().Be(10);
            result.SumSq.Should().Be(30);
        }

        [Fact]
        public async Task AggregatorRx()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var actor = new StatefulObserver<Stats, int>(default, (s, value) => new Stats(s, value));
            using var subject = new Subject<int>();
            subject.ObserveOn(Scheduler.Default).Subscribe(actor, cts.Token);
            subject.OnNext(4);
            subject.OnNext(3);
            subject.OnNext(2);
            subject.OnNext(1);
            subject.OnNext(0);
            subject.OnCompleted();

            var result = await actor.FinalState.ConfigureAwait(false);
            result.Count.Should().Be(5);
            result.Total.Should().Be(10);
            result.SumSq.Should().Be(30);
        }

    }
}