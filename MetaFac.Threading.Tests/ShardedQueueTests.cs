using FluentAssertions;
using MetaFac.Threading.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class ShardedQueueTests
    {
        private readonly struct ActorEvent
        {
            public readonly int Actor;
            public readonly int Value;
            public readonly bool Done;

            public ActorEvent(int actor, int value, bool done)
            {
                Actor = actor;
                Value = value;
                Done = done;
            }
        }

        private sealed class ShardObserver : IQueueReader<ActorEvent>, IObserver<ActorEvent>
        {
            private readonly StatefulObserver<long, int>[] _actors;

            public ShardObserver(StatefulObserver<long, int>[] actors)
            {
                _actors = actors;
            }

            public void Dispose()
            {
            }

            public void OnComplete()
            {
            }

            public void OnCompleted()
            {
            }

            public ValueTask OnDequeueAsync(ActorEvent ae)
            {
                StatefulObserver<long, int> actor = _actors[ae.Actor];
                if (ae.Done)
                    actor.OnCompleted();
                else
                    actor.OnNext(ae.Value);
                return new ValueTask();
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(ActorEvent ae)
            {
                StatefulObserver<long, int> actor = _actors[ae.Actor];
                if (ae.Done)
                    actor.OnCompleted();
                else
                    actor.OnNext(ae.Value);
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task ShardPool_ChannelQueue(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<ActorEvent>();

            const int ActorCount = 10;
            const int MaxThreads = 4;
            const int EventCount = 1000;
            var actors = new StatefulObserver<long, int>[ActorCount];
            for (int a = 0; a < ActorCount; a++)
            {
                actors[a] = new StatefulObserver<long, int>(0L, (s, e) => s + e);
            }
            var observer = new ShardObserver(actors);
            using var subjectPool = new ShardPool<ActorEvent>(queueFactory, observer, MaxThreads);

            Parallel.For(0, EventCount, async (i) =>
            {
                int a = i % ActorCount;
                await subjectPool.EnqueueAsync(a, new ActorEvent(a, i, false));
            });

            for (int a = 0; a < ActorCount; a++)
            {
                await subjectPool.EnqueueAsync(a, new ActorEvent(a, 0, true));
            }

            long result = 0;
            for (int a = 0; a < ActorCount; a++)
            {
                result += await actors[a].FinalState.ConfigureAwait(false);
            }

            result.Should().Be(499500L);
        }

        // todo more implementations
        // channels
        // overlapped?

    }
}