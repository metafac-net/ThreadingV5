using BenchmarkDotNet.Running;
using System.Reflection;

namespace MetaFac.Threading.Benchmarks
{

    public class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run();
        }
    }
}