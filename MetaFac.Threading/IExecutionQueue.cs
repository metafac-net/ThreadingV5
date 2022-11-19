using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public interface IExecutionQueue<T> where T : class, IExecutable
    {
        ValueTask EnqueueAsync(T workItem);
    }
}