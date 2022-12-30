using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public readonly struct ValueTaskItem<TInp, TOut>
    {
        public readonly TInp Input;
        public readonly CancellationToken Token;
        public readonly TaskCompletionSource<TOut>? Completion;
        public readonly Func<TInp, CancellationToken, ValueTask<TOut>> UserFunc;

        public ValueTaskItem(TInp input, CancellationToken token, Func<TInp, CancellationToken, ValueTask<TOut>> userFunc, TaskCompletionSource<TOut>? completion = null)
        {
            Input = input;
            Token = token;
            Completion = completion;
            UserFunc = userFunc;
        }
    }
}