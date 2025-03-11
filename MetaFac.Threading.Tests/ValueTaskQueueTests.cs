using Shouldly;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class ValueTaskQueueTests
    {
        private sealed class WorkItem_Good : ExecutableItemBase<bool, bool>
        {
            public WorkItem_Good(bool input) : base(input, CancellationToken.None) { }
            protected override ValueTask<bool> OnExecuteAsync() => new ValueTask<bool>(_input);
        }

        private static ValueTask<bool> GoodTask(bool input, CancellationToken token)
        {
            return new ValueTask<bool>(input);
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task ExecuteSyncWorkItem(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<ValueTaskItem<bool,bool>>();
            const int iterations = 100_000;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var queue = new ValueTaskQueue<bool,bool>(queueFactory, cts.Token))
            {
                for (int i = 0; i < iterations; i++)
                {
                    await queue.EnqueueAsync(false, CancellationToken.None, GoodTask, null);
                }

                var observer = new TaskCompletionSource<bool>();
                await queue.EnqueueAsync(true, CancellationToken.None, GoodTask, observer);

                var result = await observer.Task;
                result.ShouldBeTrue();
            }
        }

        private static ValueTask<bool> FailTask(bool input, CancellationToken token)
        {
            throw new ApplicationException("I'm a bad app!");
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task ExecuteFailingWorkItem(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<ValueTaskItem<bool, bool>>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var queue = new ValueTaskQueue<bool,bool>(queueFactory, cts.Token))
            {
                var observer = new TaskCompletionSource<bool>();
                await queue.EnqueueAsync(false, cts.Token, FailTask, observer);

                var ex = await Assert.ThrowsAsync<ApplicationException>(async () =>
                {
                    var result = await observer.Task;
                });
                ex.Message.ShouldBe("I'm a bad app!");
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task ExecuteCustomWorkItems(bool waitParallel, bool waitAfterDispose)
        {
            const int iterations = 10;

            QueueImpl impl = QueueImpl.UnboundedChannelQueue;
            var queueFactory = impl.GetFactory<ValueTaskItem<bool, bool>>();

            var goodCount = 0;
            var failCount = 0;
            var observers = new TaskCompletionSource<bool>[iterations];
            for (int i = 0; i < iterations; i++)
            {
                observers[i] = new TaskCompletionSource<bool>();
            }

            async ValueTask WaitFuncAsync(TaskCompletionSource<bool>[] tasks, int i)
            {
                try
                {
                    bool result = await observers[i].Task;
                    Interlocked.Increment(ref goodCount);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref failCount);
                }
            }

            void WaitFuncSync(TaskCompletionSource<bool>[] observers, int i)
            {
                try
                {
                    bool result = observers[i].Task.ConfigureAwait(false).GetAwaiter().GetResult();
                    Interlocked.Increment(ref goodCount);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref failCount);
                }
            }

            var timeout = Debugger.IsAttached ? TimeSpan.FromSeconds(300) : TimeSpan.FromSeconds(10);
            var cts = new CancellationTokenSource(timeout);
            var queue = new ValueTaskQueue<bool,bool>(queueFactory, cts.Token);
            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    await queue.EnqueueAsync(false, cts.Token, GoodTask, observers[i]);
                }

                if (!waitAfterDispose)
                {
                    if (waitParallel)
                    {
                        Parallel.For(0, iterations, (i) =>
                        {
                            WaitFuncSync(observers, i);
                        });
                    }
                    else
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            await WaitFuncAsync(observers, i);
                        }
                    }
                }
            }
            finally
            {
                queue.Dispose();
            }

            if (waitAfterDispose)
            {
                if (waitParallel)
                {
                    Parallel.For(0, iterations, (i) =>
                    {
                        WaitFuncSync(observers, i);
                    });
                }
                else
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        await WaitFuncAsync(observers, i);
                    }
                }
            }

            (goodCount + failCount).ShouldBe(iterations);
        }
    }
}