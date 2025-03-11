using Shouldly;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class ExecutionQueueTests
    {
        private sealed class WorkItem_Good : ExecutableItemBase<bool, bool>
        {
            public WorkItem_Good(bool input) : base(input, CancellationToken.None) { }
            protected override ValueTask<bool> OnExecuteAsync() => new ValueTask<bool>(_input);
        }

        private static bool DoWork(bool input, CancellationToken token)
        {
            return input;
        }

        private static ValueTask<bool> DoWorkAsync(bool input, CancellationToken token)
        {
            return new ValueTask<bool>(input);
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task ExecuteSyncWorkItem(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<IExecutable>();
            const int iterations = 100_000;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var queue = new ExecutionQueue<IExecutable>(queueFactory, cts.Token))
            {
                for (int i = 0; i < iterations; i++)
                {
                    await queue.EnqueueAsync(new ExecutableItem<bool, bool>(false, CancellationToken.None, DoWork));
                }

                var lastItem = new ExecutableItem<bool, bool>(true, CancellationToken.None, DoWork);
                await queue.EnqueueAsync(lastItem);

                var result = await lastItem.GetTask();
                result.ShouldBeTrue();
            }
        }

        private sealed class WorkItem_Fail : ExecutableItemBase<bool, bool>
        {
            public WorkItem_Fail(bool input) : base(input, CancellationToken.None) { }
            protected override ValueTask<bool> OnExecuteAsync() => throw new ApplicationException("I'm a bad app!");
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task ExecuteFailingWorkItem(QueueImpl impl)
        {
            var queueFactory = impl.GetFactory<WorkItem_Fail>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var queue = new ExecutionQueue<WorkItem_Fail>(queueFactory, cts.Token))
            {
                var workItem = new WorkItem_Fail(false);
                await queue.EnqueueAsync(workItem);

                var ex = await Assert.ThrowsAsync<ApplicationException>(async () =>
                {
                    var result = await workItem.GetTask();
                });
                ex.Message.ShouldBe("I'm a bad app!");
            }
        }

        private sealed class CustomWorkItem : IExecutable
        {
            private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

            private readonly TimeSpan _delay;
            private readonly bool _throwError;

            public CustomWorkItem(TimeSpan delay, bool throwError)
            {
                _delay = delay;
                _throwError = throwError;
            }

            public void Dispose()
            {
                _tcs.TrySetCanceled();
            }

            public ValueTask DisposeAsync()
            {
                _tcs.TrySetCanceled();
#if NET5_0_OR_GREATER
                return ValueTask.CompletedTask;
#else
                return new ValueTask();
#endif
            }

            public async ValueTask ExecuteAsync(CancellationToken shutdownToken)
            {
                if (_delay > TimeSpan.Zero)
                {
                    await Task.Delay(_delay);
                }
                if (_throwError)
                    throw new ApplicationException("I am a failure!");

                _tcs.TrySetResult(true);
            }

            public Task<bool> GetTask() => _tcs.Task;
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task ExecuteCustomWorkItems(bool waitParallel, bool waitAfterDispose)
        {
            const int iterations = 10;

            var queueFactory = QueueImpl.UnboundedChannelQueue.GetFactory<CustomWorkItem>();

            var goodCount = 0;
            var failCount = 0;
            var workItems = new CustomWorkItem[iterations];
            var tasks = new Task<bool>[iterations];
            for (int i = 0; i < iterations; i++)
            {
                workItems[i] = new CustomWorkItem(TimeSpan.Zero, false);
                tasks[i] = workItems[i].GetTask();
            }

            async ValueTask WaitFuncAsync(Task<bool>[] tasks, int i)
            {
                try
                {
                    bool result = await tasks[i];
                    Interlocked.Increment(ref goodCount);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref failCount);
                }
            }

            void WaitFuncSync(Task<bool>[] tasks, int i)
            {
                try
                {
                    bool result = tasks[i].ConfigureAwait(false).GetAwaiter().GetResult();
                    Interlocked.Increment(ref goodCount);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref failCount);
                }
            }

            var timeout = Debugger.IsAttached ? TimeSpan.FromSeconds(300) : TimeSpan.FromSeconds(10);
            var cts = new CancellationTokenSource(timeout);
            var queue = new ExecutionQueue<CustomWorkItem>(queueFactory, cts.Token);
            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    await queue.EnqueueAsync(workItems[i]);
                }

                if (!waitAfterDispose)
                {
                    if (waitParallel)
                    {
                        Parallel.For(0, iterations, (i) =>
                        {
                            WaitFuncSync(tasks, i);
                        });
                    }
                    else
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            await WaitFuncAsync(tasks, i);
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
                        WaitFuncSync(tasks, i);
                    });
                }
                else
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        await WaitFuncAsync(tasks, i);
                    }
                }
            }

            (goodCount + failCount).ShouldBe(iterations);
        }
    }
}