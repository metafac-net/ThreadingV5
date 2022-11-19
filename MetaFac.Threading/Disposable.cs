using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public abstract class Disposable : IDisposable, IAsyncDisposable
    {
        private readonly string? _objectName = null;

        public Disposable() { }
        public Disposable(string objectName) => _objectName = objectName;

        private volatile int _state = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryBeginDispose() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;

        protected bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _state != 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected void ThrowDisposedException()
        {
            throw new ObjectDisposedException(_objectName ?? GetType().Name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ThrowIfDisposed()
        {
            if (_state != 0) ThrowDisposedException();
        }

        protected abstract ValueTask OnDisposeAsync();

        public void Dispose()
        {
            if (TryBeginDispose())
            {
                OnDisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (TryBeginDispose())
            {
                await OnDisposeAsync().ConfigureAwait(false);
            }
            GC.SuppressFinalize(this);
        }
    }
}