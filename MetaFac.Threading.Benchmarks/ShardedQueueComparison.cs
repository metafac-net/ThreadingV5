using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net70)]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class ShardedQueueComparison
    {
        //[Params(1 * 1024 * 1024)]
        const int EventCount = 1 * 1024 * 1024;

        //[Params(64, 256, 1024)]
        //[Params(1024)]
        const int ActorCount = 1024;

        //[Params(4, 16, 64, 256)]
        [Params(4, 8, 16, 32, 64)]
        public int Shards;

        [Params(1, 2, 4, 8)]
        public int InParallelism;

        [GlobalSetup]
        public void GlobalSetup()
        {
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

            using var subjectPool = new ShardPool<ChannelQueue<ActorEvent>, ActorEvent>(
                () => new ChannelQueue<ActorEvent>(observer, CancellationToken.None),
                Shards);

            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = InParallelism };
            Parallel.For(0, EventCount, options, (i) =>
            {
                int actor = i % ActorCount;
                subjectPool.EnqueueAsync(actor, new ActorEvent(actor, false, i))
                .ConfigureAwait(false).GetAwaiter().GetResult();
            });

            for (int actor = 0; actor < ActorCount; actor++)
            {
                await subjectPool.EnqueueAsync(actor, new ActorEvent(actor, true, 0));
            }

            for (int actor = 0; actor < ActorCount; actor++)
            {
                await actors[actor].FinalState.ConfigureAwait(false);
            }
        }

        //[Benchmark(OperationsPerInvoke = EventCount)]
        public async ValueTask Sharded_SubjectQueue()
        {
            var actors = new TestActor[ActorCount];
            for (int a = 0; a < ActorCount; a++)
            {
                actors[a] = new TestActor();
            }
            ShardObserver observer = new ShardObserver(actors);

            using var subjectPool = new ShardPool<RxQueue<ActorEvent>, ActorEvent>(
                () => new RxQueue<ActorEvent>(Scheduler.Default, observer),
                Shards);

            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = InParallelism };
            Parallel.For(0, EventCount, options, (i) =>
            {
                int actor = i % ActorCount;
                subjectPool.EnqueueAsync(actor, new ActorEvent(actor, false, i))
                .ConfigureAwait(false).GetAwaiter().GetResult();
            });

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