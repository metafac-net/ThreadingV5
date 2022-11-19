using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Threading;

namespace MetaFac.Threading.Benchmarks
{

    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net60)]
    public class InterlockVsLock
    {
        [Params(1_000_000)]
        public int Iterations;

        private long counter = 0;
        private object _lock = new object();
        private object? _state = new object();

        [GlobalSetup]
        public void Setup()
        {

        }

        [Benchmark(Baseline = true)]
        public void LockObject()
        {
            for (int i = 0; i < Iterations; i++)
            {
                lock (_lock)
                {
                    counter++;
                }
            }
        }

        [Benchmark]
        public void Interlock()
        {
            for (int i = 0; i < Iterations; i++)
            {
                object? state;
                do
                {
                    state = Interlocked.Exchange(ref _state, null);
                }
                while (state is null);
                try
                {
                    counter++;
                }
                finally
                {
                    Interlocked.Exchange(ref _state, state);
                }
            }
        }

    }
}