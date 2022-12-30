using System;
using MetaFac.Threading.Core;
using Disruptor;

namespace MetaFac.Threading.Disruptor
{
    internal sealed class DisruptorEventHandler<T> : IEventHandler<DisruptorEvent<T>>
    {
        private readonly IQueueReader<T> _observer;

        public DisruptorEventHandler(IQueueReader<T> observer)
        {
            _observer = observer;
        }

        public void OnEvent(DisruptorEvent<T> data, long sequence, bool endOfBatch)
        {
            try
            {
                if (data.Complete)
                {
                    _observer.OnComplete();
                }
                else
                {
                    _observer.OnDequeueAsync(data.Value!).ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }
            catch (Exception)
            {
                // the observer should handle all their errors
            }
        }
    }

}