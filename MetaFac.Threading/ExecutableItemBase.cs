using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public abstract class ExecutableItemBase<TInp, TOut> : Disposable, IExecutable
    {
        private readonly TaskCompletionSource<TOut> _completion = new TaskCompletionSource<TOut>();
        protected readonly TInp _input;
        protected readonly CancellationToken _token;
        public Task<TOut> GetTask() => _completion.Task;

        protected ExecutableItemBase(TInp input, CancellationToken token)
        {
            _input = input;
            _token = token;
        }

        protected override ValueTask OnDisposeAsync()
        {
            _completion.TrySetCanceled(CancellationToken.None);
            return new ValueTask();
        }

        protected abstract ValueTask<TOut> OnExecuteAsync();
        async ValueTask IExecutable.ExecuteAsync(CancellationToken shutdownToken)
        {
            if (shutdownToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(shutdownToken);
            }
            else
            {
                try
                {
                    TOut output = await OnExecuteAsync().ConfigureAwait(false);
                    _completion.TrySetResult(output);
                }
                catch (Exception e)
                {
                    _completion.TrySetException(e);
                }
            }
        }
    }
}