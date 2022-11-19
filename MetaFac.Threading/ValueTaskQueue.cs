using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class ValueTaskQueue<TInp, TOut> : Disposable
    {
        private readonly struct ValueTaskItem
        {
            public readonly TInp Input;
            public readonly CancellationToken Token;
            public readonly TaskCompletionSource<TOut>? Completion;
            public readonly Func<TInp, CancellationToken, ValueTask<TOut>> UserFunc;

            public ValueTaskItem(TInp input, CancellationToken token, Func<TInp, CancellationToken, ValueTask<TOut>> userFunc, TaskCompletionSource<TOut>? completion = null)
            {
                Input = input;
                Token = token;
                Completion = completion;
                UserFunc = userFunc;
            }
        }

        private readonly CancellationToken _shutdownToken;
        private readonly Channel<ValueTaskItem> _channel;
        private readonly ChannelWriter<ValueTaskItem> _writer;

        public ValueTaskQueue(CancellationToken shutdownToken)
        {
            _shutdownToken = shutdownToken;
            var options = new UnboundedChannelOptions() { SingleReader = true, };
            _channel = Channel.CreateUnbounded<ValueTaskItem>(options);
            _writer = _channel.Writer;

            // launch single reader task
            _ = Task.Factory.StartNew(ReadLoop);
        }

        protected override ValueTask OnDisposeAsync()
        {
            _writer.TryComplete();
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
                ValueTaskItem item = new ValueTaskItem(input, token, func, completion);
                await _writer.WriteAsync(item).ConfigureAwait(false);
            }
        }

        private async ValueTask ExecuteItem(ValueTaskItem item)
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

        private async ValueTask ReadLoop()
        {
            var reader = _channel.Reader;
#if NET5_0_OR_GREATER
            await foreach (var item in reader.ReadAllAsync())
            {
                await ExecuteItem(item).ConfigureAwait(false);
            }
#else
            bool reading = true;
            while (reading)
            {
                try
                {
                    var item = await reader.ReadAsync().ConfigureAwait(false);
                    await ExecuteItem(item).ConfigureAwait(false);
                }
                catch (ChannelClosedException)
                {
                    // expected
                    reading = false;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Unhandled {e.GetType().Name} : {e.Message}");
                    reading = false;
                }
            }
#endif
        }

    }
}