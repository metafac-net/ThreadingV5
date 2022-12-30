using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using MetaFac.Threading.Channels;
using MetaFac.Threading.Core;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading.Benchmarks
{
    [MemoryDiagnoser]
    //[SimpleJob(RuntimeMoniker.Net481)]
    //[SimpleJob(RuntimeMoniker.Net60)]
    [SimpleJob(RuntimeMoniker.Net70)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class ValueTaskQueues
    {
        private const int EventCount = 1_000_000;

        //[Params(false)]
        private bool WithCancel = false;

        [GlobalSetup]
        public void Setup()
        {
        }

        private static IQueueWriter<T> QueueFactory<T>(IQueueReader<T> observer)
        {
            return new UnboundedChannelQueue<T>(observer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DoSyncFunc(int input, CancellationToken token)
        {
            return input * input;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ValueTask<int> DoValueTask(int input, CancellationToken token)
        {
            return new ValueTask<int>(input * input);
        }

        private static async Task<int> DoTask(int input, CancellationToken token)
        {
            await Task.Delay(0);
            return input * input;
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = EventCount)]
        public async Task Unobserved_ValueTaskQueue()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            using var queue = new ValueTaskQueue<int, int>(QueueFactory<ValueTaskItem<int,int>>, token);

            for (int i = 0; i < EventCount; i++)
            {
                await queue.EnqueueAsync(i, token, DoValueTask, null).ConfigureAwait(false);
            }

            var observer = new TaskCompletionSource<int>();
            await queue.EnqueueAsync(EventCount, token, DoValueTask, observer).ConfigureAwait(false);
            int result = await observer.Task.ConfigureAwait(false);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task Observed_ValueTaskQueue()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            using var queue = new ValueTaskQueue<int, int>(QueueFactory<ValueTaskItem<int, int>>, token);

            var observers = new TaskCompletionSource<int>[EventCount];
            for (int i = 0; i < EventCount; i++)
            {
                var observer = new TaskCompletionSource<int>();
                observers[i] = observer;
                await queue.EnqueueAsync(i, token, DoValueTask, observer).ConfigureAwait(false);
            }

            await Task.WhenAll(observers.Select(o => o.Task)).ConfigureAwait(false);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task ParIngress2_ValueTaskQueue()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            using var queue = new ValueTaskQueue<int, int>(QueueFactory<ValueTaskItem<int, int>>, token);

            var parOptions = new ParallelOptions() { MaxDegreeOfParallelism = 2 };
            Parallel.For(1, EventCount, parOptions, async (i) =>
            {
                await queue.EnqueueAsync(i, token, DoValueTask, null).ConfigureAwait(false);
            });

            var observer = new TaskCompletionSource<int>();
            await queue.EnqueueAsync(EventCount, token, DoValueTask, observer).ConfigureAwait(false);
            int result = await observer.Task.ConfigureAwait(false);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task ParIngress4_ValueTaskQueue()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            using var queue = new ValueTaskQueue<int, int>(QueueFactory<ValueTaskItem<int, int>>, token);

            var parOptions = new ParallelOptions() { MaxDegreeOfParallelism = 4 };
            Parallel.For(1, EventCount, parOptions, async (i) =>
            {
                await queue.EnqueueAsync(i, token, DoValueTask, null).ConfigureAwait(false);
            });

            var observer = new TaskCompletionSource<int>();
            await queue.EnqueueAsync(EventCount, token, DoValueTask, observer).ConfigureAwait(false);
            int result = await observer.Task.ConfigureAwait(false);
        }

    }
}