using System.Threading.Tasks;
using System.Threading;
using System;
using MetaFac.Threading.Core;
using Disruptor.Dsl;
using Disruptor;

namespace MetaFac.Threading.Disruptor
{

    public sealed class DisruptorQueue<T> : Disposable, IQueueWriter<T>
    {
        private readonly Disruptor<DisruptorEvent<T>> _disruptor;

        public DisruptorQueue(IQueueReader<T> observer, int capacity)
        {
            var handler = new DisruptorEventHandler<T>(observer);
            _disruptor = new Disruptor<DisruptorEvent<T>>(() => new DisruptorEvent<T>(), ringBufferSize: capacity);
            _disruptor.HandleEventsWith(handler);
            _disruptor.Start();
        }

        protected override ValueTask OnDisposeAsync()
        {
            _disruptor.Halt();
            return new ValueTask();
        }

        private void SendComplete()
        {
            using (var scope = _disruptor.PublishEvent())
            {
                var data = scope.Event();
                data.Complete = true;
            }
        }

        private volatile bool _complete;
        public bool TryComplete()
        {
            if (_disposed) return false;
            if (_complete) return false;
            _complete = true;
            SendComplete();
            return true;
        }

        public void Complete()
        {
            ThrowIfDisposed();
            if (_complete) throw new InvalidOperationException();
            SendComplete();
        }

        private void SendItem(T item)
        {
            using (var scope = _disruptor.PublishEvent())
            {
                var data = scope.Event();
                data.Complete = false;
                data.Value = item;
            }
        }

        public ValueTask EnqueueAsync(T item)
        {
            ThrowIfDisposed();
            if (_complete) throw new InvalidOperationException();
            SendItem(item);
            return new ValueTask();
        }

        public bool TryEnqueue(T item)
        {
            if (_disposed) return false;
            if (_complete) return false;
            SendItem(item);
            return true;
        }

    }

}