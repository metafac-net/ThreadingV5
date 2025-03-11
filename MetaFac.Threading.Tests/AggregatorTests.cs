using MetaFac.Threading.Channels;
using MetaFac.Threading.Disruptor;
using MetaFac.Threading.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;

namespace MetaFac.Threading.Tests
{
    public enum QueueImpl
    {
        UnboundedChannelQueue,
        BoundedChannelQueue1K,
        DisruptorQueue1K,
    }

    internal static class QueueImplementationHelper
    {
        public static Func<IQueueReader<T>, IQueueWriter<T>> GetFactory<T>(this QueueImpl impl)
        {
            switch (impl)
            {
                case QueueImpl.UnboundedChannelQueue:
                    return (reader) => new UnboundedChannelQueue<T>(reader);
                case QueueImpl.BoundedChannelQueue1K:
                    return (reader) => new BoundedChannelQueue<T>(reader, 1024);
                case QueueImpl.DisruptorQueue1K:
                    return (reader) => new DisruptorQueue<T>(reader, 1024);
                default:
                    throw new ArgumentOutOfRangeException(nameof(impl), impl, null);
            }
        }
    }

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

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task Aggregator(QueueImpl impl)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var queueFactory = impl.GetFactory<int>();
            var fsm = new Aggregator<Stats, int>(default, (s, value) => new Stats(s, value), queueFactory);
            await fsm.EnqueueAsync(4);
            await fsm.EnqueueAsync(3);
            await fsm.EnqueueAsync(2);
            await fsm.EnqueueAsync(1);
            await fsm.EnqueueAsync(0);
            fsm.Complete();
            var result = await fsm.FinalState;
            result.Count.ShouldBe(5);
            result.Total.ShouldBe(10);
            result.SumSq.ShouldBe(30);
        }
    }
}