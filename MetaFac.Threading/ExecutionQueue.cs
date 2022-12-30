using MetaFac.Threading.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    internal sealed class ExecutionQueueReader<T> : Disposable, IQueueReader<T>
        where T : class, IExecutable
    {
        private readonly CancellationToken _shutdownToken;

        public ExecutionQueueReader(CancellationToken shutdownToken)
        {
            _shutdownToken = shutdownToken;
        }

        protected override ValueTask OnDisposeAsync() => new ValueTask();

        private async ValueTask ExecuteItem(T item)
        {
            try
            {
                await item.ExecuteAsync(_shutdownToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
            try
            {
                await item.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        public void OnComplete() { }
        public ValueTask OnDequeueAsync(T item)
        {
            return ExecuteItem(item);
        }
    }

    public sealed class ExecutionQueue<T> : Disposable, IQueueWriter<T>
        where T : class, IExecutable
    {
        private readonly IQueueWriter<T> _queue;

        public ExecutionQueue(Func<IQueueReader<T>, IQueueWriter<T>> queueFactory, CancellationToken shutdownToken)
        {
            var reader = new ExecutionQueueReader<T>(shutdownToken);
            _queue = queueFactory(reader);
        }

        protected override ValueTask OnDisposeAsync()
        {
            _queue.Dispose();
            return new ValueTask();
        }

        public ValueTask EnqueueAsync(T item) => _queue.EnqueueAsync(item);
        public bool TryEnqueue(T item) => _queue.TryEnqueue(item);
        public void Complete() => _queue.Complete();
        public bool TryComplete() => _queue.TryComplete();
    }
}