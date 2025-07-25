namespace Chuncker.Interfaces
{
    /// <summary>
    /// Event işleyici arayüzü
    /// </summary>
    /// <typeparam name="TEvent">İşlenecek event türü</typeparam>
    public interface IEventHandler<TEvent> where TEvent : IEvent
    {
        /// <summary>
        /// Event'i işler
        /// </summary>
        /// <param name="event">İşlenecek event</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem sonucu</returns>
        Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
    }
}
