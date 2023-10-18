using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using MetaFac.Threading.Channels;
using MetaFac.Threading.Core;
using System;
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
    public class ExecutionQueues
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

        private static Task<int> DoTask(int input, CancellationToken token)
        {
            return Task.FromResult(input * input);
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = EventCount)]
        public async Task ValueTask_ExecutionQueue()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            using var queue = new ExecutionQueue<IExecutable>(QueueFactory<IExecutable>, cts.Token);

            for (int i = 0; i < EventCount; i++)
            {
                await queue.EnqueueAsync(new ExecutableItem<int, int>(i, token, DoValueTask)).ConfigureAwait(false);
            }

            var lastTask = new ExecutableItem<int, int>(EventCount, token, DoValueTask);
            await queue.EnqueueAsync(lastTask).ConfigureAwait(false);
            int result = await lastTask.GetTask().ConfigureAwait(false);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task Observed_ExecutionQueue()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            using var queue = new ExecutionQueue<IExecutable>(QueueFactory<IExecutable>, token);

            var tasks = new Task<int>[EventCount];
            for (int i = 0; i < EventCount; i++)
            {
                var item = new ExecutableItem<int, int>(i, token, DoValueTask);
                tasks[i] = item.GetTask();
                await queue.EnqueueAsync(item).ConfigureAwait(false);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task Sync_ExecutionQueue()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            using var queue = new ExecutionQueue<IExecutable>(QueueFactory<IExecutable>, token);

            for (int i = 0; i < EventCount; i++)
            {
                await queue.EnqueueAsync(new ExecutableItem<int, int>(i, token, DoSyncFunc)).ConfigureAwait(false);
            }

            var lastTask = new ExecutableItem<int, int>(EventCount, token, DoSyncFunc);
            await queue.EnqueueAsync(lastTask).ConfigureAwait(false);
            int result = await lastTask.GetTask().ConfigureAwait(false);
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task Task_ExecutionQueue()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = WithCancel ? cts.Token : CancellationToken.None;
            using var queue = new ExecutionQueue<IExecutable>(QueueFactory<IExecutable>, token);

            for (int i = 0; i < EventCount; i++)
            {
                await queue.EnqueueAsync(new ExecutableItem<int, int>(i, token, DoTask)).ConfigureAwait(false);
            }

            var lastTask = new ExecutableItem<int, int>(EventCount, token, DoTask);
            await queue.EnqueueAsync(lastTask).ConfigureAwait(false);
            int result = await lastTask.GetTask().ConfigureAwait(false);
        }

    }
}