using MetaFac.Threading;
using MetaFac.Threading.Benchmarks;
using System;

namespace MetaFac.Threading.Benchmarks
{
    public static class ShardHelper
    {
        internal sealed class ShardObserver : IObserver<ActorEvent>
        {
            private readonly StatefulObserver<long, int>[] _actors;

            public ShardObserver(StatefulObserver<long, int>[] actors)
            {
                _actors = actors;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(ActorEvent ae)
            {
                StatefulObserver<long, int> actor = _actors[ae.ActorNum];
                if (ae.Done)
                    actor.OnCompleted();
                else
                    actor.OnNext(ae.Value);
            }
        }
    }
}