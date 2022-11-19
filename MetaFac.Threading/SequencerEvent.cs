namespace MetaFac.Threading
{
    public enum SequencerEventKind
    {
        Undefined,
        EmptyRequest,
        EmptyResponse,
        WorkItem,
    }
    public readonly struct SequencerEvent
    {
        public readonly SequencerEventKind Kind;
        public readonly int Slot;
        public readonly int Level { get; }
        public readonly int[]? Keys { get; }
        public readonly IExecutable? WorkItem { get; }

        public SequencerEvent(SequencerEventKind kind, int slot)
        {
            Kind = kind;
            Slot = slot;
            Level = 0;
            Keys = null;
            WorkItem = null;
        }
        public SequencerEvent(int level, int[] keys, IExecutable workItem)
        {
            Kind = SequencerEventKind.WorkItem;
            Slot = 0;
            Level = level;
            Keys = keys;
            WorkItem = workItem;
        }
    }
}
