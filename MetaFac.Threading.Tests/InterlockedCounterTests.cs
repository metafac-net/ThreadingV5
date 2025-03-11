using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class InterlockedCounterTests
    {
        private const int EventCount = 1_000_000;
        private const int SubNodes = 10;

        [Fact]
        public void InterlockedParent()
        {
            var L0Node = new InterlockedCounter(null);
            var L1Nodes = new InterlockedCounter[SubNodes];
            var L2Nodes = new InterlockedCounter[SubNodes * SubNodes];
            var L3Nodes = new InterlockedCounter[SubNodes * SubNodes * SubNodes];
            var L4Nodes = new InterlockedCounter[SubNodes * SubNodes * SubNodes * SubNodes];
            for (int i = 0; i < SubNodes; i++)
            {
                var l1 = i;
                L1Nodes[l1] = new InterlockedCounter(L0Node);
                for (int j = 0; j < SubNodes; j++)
                {
                    var l2 = l1 * SubNodes + j;
                    L2Nodes[l2] = new InterlockedCounter(L1Nodes[l1]);
                    for (int k = 0; k < SubNodes; k++)
                    {
                        var l3 = l2 * SubNodes + k;
                        L3Nodes[l3] = new InterlockedCounter(L2Nodes[l2]);
                        for (int l = 0; l < SubNodes; l++)
                        {
                            var l4 = l3 * SubNodes + l;
                            L4Nodes[l4] = new InterlockedCounter(L3Nodes[l3]);
                        }
                    }
                }
            }
            int l4NodeCount = (SubNodes * SubNodes * SubNodes * SubNodes);
            Parallel.For(0, EventCount, (n) =>
            {
                int l4 = n % l4NodeCount;
                L4Nodes[l4].Add(1);
            });

            L0Node.Count.ShouldBe(EventCount);
        }

    }
}