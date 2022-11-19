using MetaFac.Threading;
using MetaFac.Threading.Benchmarks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading.Benchmarks
{
    internal sealed class ShardObserver : IObserver<ActorEvent>, IQueueReader<ActorEvent>
    {
        private readonly TestActor[] _actors;

        public ShardObserver(TestActor[] actors)
        {
            _actors = actors;
        }

        public void Dispose()
        {
        }

        public void OnComplete()
        {
        }

        public void OnCompleted()
        {
        }

        public ValueTask OnDequeueAsync(ActorEvent ae)
        {
            TestActor actor = _actors[ae.ActorNum];
            if (ae.Done)
                actor.OnCompleted();
            else
                actor.OnNext(ae);
            return new ValueTask();
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(ActorEvent ae)
        {
            TestActor actor = _actors[ae.ActorNum];
            if (ae.Done)
                actor.OnCompleted();
            else
                actor.OnNext(ae);
        }
    }
}