using MetaFac.Threading.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{

    public sealed class ValueTaskQueue<TInp, TOut> : Disposable, IQueueReader<ValueTaskItem<TInp, TOut>>
    {
        private readonly CancellationToken _shutdownToken;
        private readonly IQueueWriter<ValueTaskItem<TInp, TOut>> _queue;

        public ValueTaskQueue(
            Func<IQueueReader<ValueTaskItem<TInp, TOut>>, IQueueWriter<ValueTaskItem<TInp, TOut>>> queueFactory,
            CancellationToken token)
        {
            _queue = queueFactory(this);
            _shutdownToken = token;
        }

        protected override ValueTask OnDisposeAsync()
        {
            _queue.Dispose();
            return new ValueTask();
        }

        public async ValueTask EnqueueAsync(TInp input, CancellationToken token, Func<TInp, CancellationToken, ValueTask<TOut>> func, TaskCompletionSource<TOut>? completion = null)
        {
            if (func is null) throw new ArgumentNullException(nameof(func));

            ThrowIfDisposed();

            if (_shutdownToken.IsCancellationRequested)
            {
                completion?.TrySetCanceled(_shutdownToken);
            }
            else
            {
                ValueTaskItem<TInp, TOut> item = new ValueTaskItem<TInp, TOut>(input, token, func, completion);
                await _queue.EnqueueAsync(item).ConfigureAwait(false);
            }
        }

        private async ValueTask ExecuteItem(ValueTaskItem<TInp, TOut> item)
        {
            if (_shutdownToken.IsCancellationRequested)
            {
                item.Completion?.TrySetCanceled(_shutdownToken);
            }
            else
            {
                try
                {
                    TOut output = await item.UserFunc(item.Input, item.Token).ConfigureAwait(false);
                    item.Completion?.TrySetResult(output);
                }
                catch (Exception e)
                {
                    item.Completion?.TrySetException(e);
                }
            }
        }

        public ValueTask OnDequeueAsync(ValueTaskItem<TInp, TOut> item)
        {
            return ExecuteItem(item);
        }

        public void OnComplete()
        {
            // not used
        }
    }
}