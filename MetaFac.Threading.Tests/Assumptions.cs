using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class Assumptions
    {
        [Fact]
        public void DefaultValueTaskIsCompleted()
        {
            var result = new ValueTask();
            result.IsCompleted.ShouldBeTrue();
            result.IsCompletedSuccessfully.ShouldBeTrue();
            result.IsCanceled.ShouldBeFalse();
            result.IsFaulted.ShouldBeFalse();
        }
    }
}