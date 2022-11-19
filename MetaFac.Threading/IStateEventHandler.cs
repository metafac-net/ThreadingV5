namespace MetaFac.Threading
{
    public interface IStateEventHandler<TState, TEvent>
    {
        TState HandleEvent(TState state, TEvent @event);
        TState CancelEvent(TState state, TEvent @event);
    }
}
