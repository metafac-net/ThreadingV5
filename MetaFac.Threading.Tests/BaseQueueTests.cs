using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class BaseQueueTests
    {
        [Fact]
        public async Task BaseQueue_Cancel1_EnqueueBeforeCancel()
        {
            using var cts = new CancellationTokenSource();
            using (var queue = new ExecutionQueue<TestWorkItem>(cts.Token))
            {
                var workItem = new TestWorkItem(true, CancellationToken.None);
                await queue.EnqueueAsync(workItem).ConfigureAwait(false);

                // ensure item executes
                var result = await workItem.GetTask().ConfigureAwait(false);

                // cancel
                cts.Cancel();

                result.Should().BeTrue();
            }
        }

        [Fact]
        public async Task BaseQueue_Cancel2_EnqueueAfterCancel()
        {
            using var cts = new CancellationTokenSource();
            using (var queue = new ExecutionQueue<TestWorkItem>(cts.Token))
            {
                // cancel
                cts.Cancel();

                var workItem = new TestWorkItem(true, CancellationToken.None);
                await queue.EnqueueAsync(workItem).ConfigureAwait(false);
                var ex = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    var result = await workItem.GetTask().ConfigureAwait(false);
                }).ConfigureAwait(false);
                ex.Message.Should().Be("A task was canceled.");
            }
        }

        [Fact]
        public async Task BaseQueue_Dispose1_EnqueueBeforeDispose()
        {
            using (var queue = new ExecutionQueue<TestWorkItem>(CancellationToken.None))
            {
                var workItem = new TestWorkItem(true, CancellationToken.None);
                await queue.EnqueueAsync(workItem).ConfigureAwait(false);

                // ensure item executes
                var result = await workItem.GetTask().ConfigureAwait(false);

                // dispose
                queue.Dispose();

                result.Should().BeTrue();
            }
        }

        [Fact]
        public async Task BaseQueue_Dispose2_EnqueueAfterDispose()
        {
            using (var queue = new ExecutionQueue<TestWorkItem>(CancellationToken.None))
            {
                // dispose
                queue.Dispose();

                var workItem = new TestWorkItem(true, CancellationToken.None);
                await queue.EnqueueAsync(workItem).ConfigureAwait(false);
                var ex = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    var result = await workItem.GetTask().ConfigureAwait(false);
                }).ConfigureAwait(false);
                ex.Message.Should().Be("A task was canceled.");
            }
        }

    }
}