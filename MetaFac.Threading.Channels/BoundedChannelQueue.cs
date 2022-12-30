using MetaFac.Threading.Core;
using System.Threading;
using System.Threading.Channels;

namespace MetaFac.Threading.Channels
{
    public sealed class BoundedChannelQueue<T> : ChannelQueueBase<T>
    {
        public BoundedChannelQueue(IQueueReader<T> observer, int channelCapacity)
            : base(observer, Channel.CreateBounded<T>(new BoundedChannelOptions(channelCapacity) { SingleReader = true }))
        {
        }
    }
}