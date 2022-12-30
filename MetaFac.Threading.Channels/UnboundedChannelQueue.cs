using MetaFac.Threading.Core;
using System.Threading;
using System.Threading.Channels;

namespace MetaFac.Threading.Channels
{
    public sealed class UnboundedChannelQueue<T> : ChannelQueueBase<T>
    {
        public UnboundedChannelQueue(IQueueReader<T> observer)
            : base(observer, Channel.CreateUnbounded<T>(new UnboundedChannelOptions() { SingleReader = true }))
        {
        }
    }
}