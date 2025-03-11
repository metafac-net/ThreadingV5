using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class AwaitableCounterTests
    {
        [Fact]
        public async Task SignalledWhenCreated()
        {
            var cdt = new AwaitableCounter();
            bool result = await cdt.UntilZero;
            result.ShouldBeTrue();
        }

        [Fact]
        public void NotSignalledWhenPositive()
        {
            var cdt = new AwaitableCounter(1);
            (long count, bool complete) = cdt.State;
            count.ShouldBe(1);
            complete.ShouldBeFalse();
        }

        [Fact]
        public void NotSignalledWhenNegative()
        {
            var cdt = new AwaitableCounter(-1);
            (long count, bool complete) = cdt.State;
            count.ShouldBe(-1);
            complete.ShouldBeFalse();
        }

        [Fact]
        public async Task SignalledWhenDecreasesToZero()
        {
            var cdt = new AwaitableCounter(1);
            cdt.Decrement();
            bool result = await cdt.UntilZero;
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task SignalledWhenIncreasesToZero()
        {
            var cdt = new AwaitableCounter(-1);
            cdt.Increment();
            bool result = await cdt.UntilZero;
            result.ShouldBe(false);
        }

        [Fact]
        public async Task SignalledWhenDecreasesToZeroByOtherTask()
        {
            var cdt = new AwaitableCounter();
            cdt.Increment();
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                cdt.Decrement();
            });
            bool result = await cdt.UntilZero;
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task SignalledWhenIncreasesToZeroByOtherTask()
        {
            var cdt = new AwaitableCounter();
            cdt.Decrement();
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                cdt.Increment();
            });
            bool result = await cdt.UntilZero;
            result.ShouldBe(false);
        }

    }
}