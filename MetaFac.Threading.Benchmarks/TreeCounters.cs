#nullable enable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MetaFac.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net60)]
    public class TreeCounters
    {
        [Params(1_000_000)]
        public int EventCount;

        [Params(10)]
        public int SubNodes;

        private InterlockedCounter? L0_ICNode;
        private InterlockedCounter[]? L1_ICNodes;
        private InterlockedCounter[]? L2_ICNodes;
        private InterlockedCounter[]? L3_ICNodes;
        private InterlockedCounter[]? L4_ICNodes;

        [GlobalSetup]
        public void Setup()
        {
            L0_ICNode = new InterlockedCounter(null);
            L1_ICNodes = new InterlockedCounter[SubNodes];
            L2_ICNodes = new InterlockedCounter[SubNodes * SubNodes];
            L3_ICNodes = new InterlockedCounter[SubNodes * SubNodes * SubNodes];
            L4_ICNodes = new InterlockedCounter[SubNodes * SubNodes * SubNodes * SubNodes];
            for (int I = 0; I < SubNodes; I++)
            {
                var L1 = I;
                L1_ICNodes[L1] = new InterlockedCounter(L0_ICNode);
                for (int J = 0; J < SubNodes; J++)
                {
                    var L2 = L1 * SubNodes + J;
                    L2_ICNodes[L2] = new InterlockedCounter(L1_ICNodes[L1]);
                    for (int K = 0; K < SubNodes; K++)
                    {
                        var L3 = L2 * SubNodes + K;
                        L3_ICNodes[L3] = new InterlockedCounter(L2_ICNodes[L2]);
                        for (int L = 0; L < SubNodes; L++)
                        {
                            var L4 = L3 * SubNodes + L;
                            L4_ICNodes[L4] = new InterlockedCounter(L3_ICNodes[L3]);
                        }
                    }
                }
            }

        }

        [Benchmark(Baseline = true)]
        public void InterlockedCounter()
        {
            if (L4_ICNodes is null) return;
            int L4NodeCount = L4_ICNodes.Length;
            Parallel.For(0, EventCount, (n) =>
            {
                int L4 = n % L4NodeCount;
                L4_ICNodes[L4].Add(1);
            });
        }

    }
}