using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading.Tests
{
    public sealed class TestWorkItem : ExecutableItemBase<bool, bool>
    {
        public TestWorkItem(bool input, CancellationToken token) : base(input, token)
        {
        }

        protected override ValueTask<bool> OnExecuteAsync()
        {
            return new ValueTask<bool>(!_token.IsCancellationRequested);
        }
    }

}