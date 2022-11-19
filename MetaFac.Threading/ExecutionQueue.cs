using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class ExecutionQueue<T> : Disposable, IExecutionQueue<T>
        where T : class, IExecutable
    {
        private readonly CancellationToken _shutdownToken;
        private readonly Channel<T> _channel;
        private readonly ChannelWriter<T> _writer;

        public ExecutionQueue(CancellationToken shutdownToken)
        {
            _shutdownToken = shutdownToken;
            var options = new UnboundedChannelOptions() { SingleReader = true, };
            _channel = Channel.CreateUnbounded<T>(options);
            _writer = _channel.Writer;

            // launch single reader task
            _ = Task.Factory.StartNew(EventHandler);
        }

        protected override ValueTask OnDisposeAsync()
        {
            _writer.TryComplete();
            return new ValueTask();
        }

        public async ValueTask EnqueueAsync(T item)
        {
            if (_shutdownToken.IsCancellationRequested || IsDisposed)
            {
                try
                {
                    await item.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
            else
            {
                await _writer.WriteAsync(item).ConfigureAwait(false);
            }
        }

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

        private async ValueTask EventHandler()
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