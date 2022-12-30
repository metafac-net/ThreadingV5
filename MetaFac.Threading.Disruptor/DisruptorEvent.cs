namespace MetaFac.Threading.Disruptor
{
    internal sealed class DisruptorEvent<T>
    {
        public bool Complete { get; set; }
        public T? Value { get; set; }
    }

}