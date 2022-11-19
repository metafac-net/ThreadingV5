namespace MetaFac.Threading
{
    public interface IEventHandler<TEvent>
    {
        void HandleEvent(TEvent @event);
        void CancelEvent(TEvent @event);
    }
}
