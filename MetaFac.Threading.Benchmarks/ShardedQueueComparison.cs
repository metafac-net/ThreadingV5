using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MetaFac.Threading.Channels;
using MetaFac.Threading.Core;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.SlowestToFastest)]
    public class ShardedQueueComparison
    {
        const int EventCount = 2 * 1024 * 1024;
        const int ActorCount = 1024;

        [Params(4, 5, 6, 7, 8)]
        public int Shards;

        [Params(1, 2, 3, 4, 5)]
        public int Clients;

        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        private static IQueueWriter<T> QueueFactory<T>(IQueueReader<T> observer)
        {
            return new UnboundedChannelQueue<T>(observer);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async ValueTask Sharded_ChannelQueue()
        {
            var actors = new TestActor[ActorCount];
            for (int a = 0; a < ActorCount; a++)
            {
                actors[a] = new TestActor();
            }
            ShardObserver observer = new ShardObserver(actors);

            using var subjectPool = new ShardPool<ActorEvent>(QueueFactory<ActorEvent>, observer, Shards);

            if (Clients == 1)
            {
                for (int i = 0; i < EventCount; i++)
                {
                    int actor = i % ActorCount;
                    bool sent = subjectPool.TryEnqueue(actor, new ActorEvent(actor, false, i));
                }
            }
            else
            {
                ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = Clients };
                Parallel.For(0, EventCount, options, (i) =>
                {
                    int actor = i % ActorCount;
                    bool sent = subjectPool.TryEnqueue(actor, new ActorEvent(actor, false, i));
                });
            }

            for (int actor = 0; actor < ActorCount; actor++)
            {
                await subjectPool.EnqueueAsync(actor, new ActorEvent(actor, true, 0));
            }

            for (int actor = 0; actor < ActorCount; actor++)
            {
                await actors[actor].FinalState.ConfigureAwait(false);
            }
        }

    }
}