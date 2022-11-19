using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public interface IExecutable : IDisposable, IAsyncDisposable
    {
        ValueTask ExecuteAsync(CancellationToken shutdownToken);
    }
}