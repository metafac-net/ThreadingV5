using MetaFac.Threading;
using MetaFac.Threading.Benchmarks;

namespace MetaFac.Threading.Benchmarks
{
    internal class TestActor : StatefulObserver<long, ActorEvent>
    {
        private static long handler(long state, ActorEvent @event)
        {
            return state + @event.Value;
        }
        public TestActor() : base(0L, handler) { }
    }
}