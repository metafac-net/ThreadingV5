using MetaFac.Threading.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MetaFac.Threading.Channels
{
    public abstract class ChannelQueueBase<T> : Disposable, IQueueWriter<T>
    {
        private readonly IQueueReader<T> _observer;
        private readonly ChannelReader<T> _reader;
        private readonly ChannelWriter<T> _writer;

        protected ChannelQueueBase(IQueueReader<T> observer, Channel<T> channel)
        {
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));

            _reader = channel.Reader;
            _writer = channel.Writer;

            // launch single reader task
            _ = Task.Factory.StartNew(EventHandler);
        }

        protected sealed override ValueTask OnDisposeAsync()
        {
            Complete();
            return new ValueTask();
        }

        public void Complete()
        {
            _writer.TryComplete();
        }

        public bool TryComplete()
        {
            return _writer.TryComplete();
        }

        public bool TryEnqueue(T item)
        {
            return _writer.TryWrite(item);
        }

        public async ValueTask EnqueueAsync(T item)
        {
            ThrowIfDisposed();
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
            await foreach (var item in _reader.ReadAllAsync())
            {
                await OnObserverEvent(item).ConfigureAwait(false);
            }
#else
            bool reading = true;
            while (reading)
            {
                try
                {
                    T item = await _reader.ReadAsync().ConfigureAwait(false);
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