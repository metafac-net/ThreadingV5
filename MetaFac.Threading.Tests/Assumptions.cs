using FluentAssertions;
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
            result.IsCompleted.Should().BeTrue();
            result.IsCompletedSuccessfully.Should().BeTrue();
            result.IsCanceled.Should().BeFalse();
            result.IsFaulted.Should().BeFalse();
        }
    }
}