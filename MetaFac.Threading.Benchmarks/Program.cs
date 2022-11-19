using BenchmarkDotNet.Running;

namespace MetaFac.Threading.Benchmarks
{

    public class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<ValueTaskQueues>();
        }
    }
}