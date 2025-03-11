using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class BaseQueueTests
    {
        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_01_Enqueue(QueueImpl impl)
        {
            using var cts = new CancellationTokenSource();
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using var queue = new ExecutionQueue<TestWorkItem>(queueFactory, cts.Token);

            // enqueue
            var workItem = new TestWorkItem(true, CancellationToken.None);
            await queue.EnqueueAsync(workItem);
            var result = await workItem.GetTask();
            result.ShouldBeTrue();
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_02a_Complete(QueueImpl impl)
        {
            using var cts = new CancellationTokenSource();
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using var queue = new ExecutionQueue<TestWorkItem>(queueFactory, cts.Token);

            // enqueue
            var workItem = new TestWorkItem(true, CancellationToken.None);
            await queue.EnqueueAsync(workItem);
            await workItem.GetTask();

            queue.Complete();
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_02b_CompleteTwiceIsBenign(QueueImpl impl)
        {
            using var cts = new CancellationTokenSource();
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using var queue = new ExecutionQueue<TestWorkItem>(queueFactory, cts.Token);

            // enqueue
            var workItem = new TestWorkItem(true, CancellationToken.None);
            await queue.EnqueueAsync(workItem);
            await workItem.GetTask();

            queue.Complete();
            queue.Complete();
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_03a_TryComplete(QueueImpl impl)
        {
            using var cts = new CancellationTokenSource();
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using var queue = new ExecutionQueue<TestWorkItem>(queueFactory, cts.Token);

            // enqueue
            var workItem = new TestWorkItem(true, CancellationToken.None);
            await queue.EnqueueAsync(workItem);
            await workItem.GetTask();

            bool complete = queue.TryComplete();
            complete.ShouldBeTrue();

        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_03b_TryCompleteTwice(QueueImpl impl)
        {
            using var cts = new CancellationTokenSource();
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using var queue = new ExecutionQueue<TestWorkItem>(queueFactory, cts.Token);

            // enqueue
            var workItem = new TestWorkItem(true, CancellationToken.None);
            await queue.EnqueueAsync(workItem);
            await workItem.GetTask();

            bool complete = queue.TryComplete();
            complete.ShouldBeTrue();

            complete = queue.TryComplete();
            complete.ShouldBeFalse();

        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_05a_EnqueueBeforeCancel(QueueImpl impl)
        {
            using var cts = new CancellationTokenSource();
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using (var queue = new ExecutionQueue<TestWorkItem>(queueFactory, cts.Token))
            {
                var workItem = new TestWorkItem(true, CancellationToken.None);
                await queue.EnqueueAsync(workItem);
                await workItem.GetTask();

                // cancel
                cts.Cancel();
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_05b_EnqueueAfterCancel(QueueImpl impl)
        {
            using var cts = new CancellationTokenSource();
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using (var queue = new ExecutionQueue<TestWorkItem>(queueFactory, cts.Token))
            {
                // cancel
                cts.Cancel();

                var workItem = new TestWorkItem(true, CancellationToken.None);
                await queue.EnqueueAsync(workItem);
                var ex = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    var result = await workItem.GetTask().ConfigureAwait(false);
                });
                ex.Message.ShouldBe("A task was canceled.");
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_07a_Dispose(QueueImpl impl)
        {
            var token = CancellationToken.None;
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using (var queue = new ExecutionQueue<TestWorkItem>(queueFactory, token))
            {
                var workItem = new TestWorkItem(true, CancellationToken.None);
                await queue.EnqueueAsync(workItem);
                await workItem.GetTask();

                // dispose
                queue.Dispose();
            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_07b_DisposeTwiceIsBenign(QueueImpl impl)
        {
            var token = CancellationToken.None;
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using (var queue = new ExecutionQueue<TestWorkItem>(queueFactory, token))
            {
                var workItem = new TestWorkItem(true, CancellationToken.None);
                await queue.EnqueueAsync(workItem);
                await workItem.GetTask();

                // dispose
                queue.Dispose();
                queue.Dispose();

            }
        }

        [Theory]
        [InlineData(QueueImpl.UnboundedChannelQueue)]
        [InlineData(QueueImpl.BoundedChannelQueue1K)]
        [InlineData(QueueImpl.DisruptorQueue1K)]
        public async Task BaseQueue_08_EnqueueAfterDispose(QueueImpl impl)
        {
            var token = CancellationToken.None;
            var queueFactory = impl.GetFactory<TestWorkItem>();
            using (var queue = new ExecutionQueue<TestWorkItem>(queueFactory, token))
            {
                // dispose
                queue.Dispose();

                var workItem = new TestWorkItem(true, CancellationToken.None);
                var ex = await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                {
                    await queue.EnqueueAsync(workItem).ConfigureAwait(false);
                    var result = await workItem.GetTask().ConfigureAwait(false);
                });
                ex.Message.ShouldStartWith("Cannot access a disposed object.");
            }
        }

    }
}