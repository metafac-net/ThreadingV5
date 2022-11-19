using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class RxQueue<T> : IQueueWriter<T>
    {
        private readonly Subject<T> _subject;
        private readonly IDisposable _subscription;

        public RxQueue(IScheduler scheduler, IObserver<T> observer)
        {
            _subject = new Subject<T>();
            _subscription = _subject.ObserveOn(scheduler).Subscribe(observer);
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _subject.Dispose();
        }

        public void Complete()
        {
            _subject.OnCompleted();
        }

        public ValueTask EnqueueAsync(T item)
        {
            _subject.OnNext(item);
            return new ValueTask();
        }
    }
}