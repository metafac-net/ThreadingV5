using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MetaFac.Threading.Core
{
    public abstract class Disposable : IDisposable, IAsyncDisposable
    {
        private readonly string? _objectName = null;

        public Disposable() { }
        public Disposable(string objectName) => _objectName = objectName;

        protected volatile bool _disposed = false;

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected void ThrowDisposedException()
        {
            throw new ObjectDisposedException(_objectName ?? GetType().Name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ThrowIfDisposed()
        {
            if (_disposed) ThrowDisposedException();
        }

        protected abstract ValueTask OnDisposeAsync();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OnDisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await OnDisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}