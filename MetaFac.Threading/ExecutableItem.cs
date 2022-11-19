using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class ExecutableItem<TInp, TOut> : ExecutableItemBase<TInp, TOut>
    {
        private readonly Func<TInp, CancellationToken, ValueTask<TOut>>? _valueTask = null;
        private readonly Func<TInp, CancellationToken, Task<TOut>>? _task = null;
        private readonly Func<TInp, CancellationToken, TOut>? _syncFunc = null;

        public ExecutableItem(TInp input, CancellationToken token, Func<TInp, CancellationToken, TOut> syncFunc)
            : base(input, token)
        {
            _syncFunc = syncFunc ?? throw new ArgumentNullException(nameof(syncFunc));
        }

        public ExecutableItem(TInp input, CancellationToken token, Func<TInp, CancellationToken, ValueTask<TOut>> valueTask)
            : base(input, token)
        {
            _valueTask = valueTask ?? throw new ArgumentNullException(nameof(valueTask));
        }

        public ExecutableItem(TInp input, CancellationToken token, Func<TInp, CancellationToken, Task<TOut>> task)
            : base(input, token)
        {
            _task = task ?? throw new ArgumentNullException(nameof(task));
        }

        protected override ValueTask<TOut> OnExecuteAsync()
        {
            if (_syncFunc is not null)
            {
                return new ValueTask<TOut>(_syncFunc(_input, _token));
            }
            else if (_valueTask is not null)
            {
                return _valueTask(_input, _token);
            }
            else
            {
                return new ValueTask<TOut>(_task!(_input, _token));
            }
        }
    }
}