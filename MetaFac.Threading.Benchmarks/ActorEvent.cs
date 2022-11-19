namespace MetaFac.Threading.Benchmarks
{
    public readonly struct ActorEvent
    {
        public readonly int ActorNum;
        public readonly bool Done;
        public readonly int Value;

        public ActorEvent(int actorNum, bool done, int value)
        {
            ActorNum = actorNum;
            Done = done;
            Value = value;
        }
    }
}