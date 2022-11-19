using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MetaFac.Threading
{

    public sealed class ChannelQueue<T> : Disposable, IQueueWriter<T>
    {
        private readonly CancellationToken _shutdownToken;
        private readonly IQueueReader<T> _observer;
        private readonly ChannelReader<T> _reader;
        private readonly ChannelWriter<T> _writer;

        public ChannelQueue(IQueueReader<T> observer, CancellationToken shutdownToken)
        {
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
            _shutdownToken = shutdownToken;

            var options = new UnboundedChannelOptions() { SingleReader = true, };
            var channel = Channel.CreateUnbounded<T>(options);
            _reader = channel.Reader;
            _writer = channel.Writer;

            // launch single reader task
            _ = Task.Factory.StartNew(EventHandler);
        }

        protected override ValueTask OnDisposeAsync()
        {
            Complete();
            return new ValueTask();
        }

        private volatile bool _complete;
        public void Complete()
        {
            if (_complete) return;
            _complete = true;
            _writer.TryComplete();
        }

        public async ValueTask EnqueueAsync(T item)
        {
            ThrowIfDisposed();
            _shutdownToken.ThrowIfCancellationRequested();
            if (_complete) throw new InvalidOperationException();
            await _writer.WriteAsync(item).ConfigureAwait(false);
        }

        private async ValueTask OnObserverEvent(T @event)
        {
            try
            {
                await _observer.OnDequeueAsync(@event).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // the observer should handle all their errors
            }
        }

        private void OnObserverComplete()
        {
            try
            {
                _observer.OnComplete();
            }
            catch (Exception)
            {
                // the observer should handle all their errors
            }
        }

        private async ValueTask EventHandler()
        {
#if NET5_0_OR_GREATER
            await foreach (var item in _reader.ReadAllAsync(_shutdownToken))
            {
                await OnObserverEvent(item).ConfigureAwait(false);
            }
#else
            bool reading = true;
            while (reading)
            {
                try
                {
                    T item = await _reader.ReadAsync(_shutdownToken).ConfigureAwait(false);
                    await OnObserverEvent(item).ConfigureAwait(false);
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
            OnObserverComplete();
        }

    }
}