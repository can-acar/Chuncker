namespace Chuncker.Interfaces
{
    /// <summary>
    /// Event yayınlayıcı arayüzü
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Bir event'i yayınlar
        /// </summary>
        /// <typeparam name="TEvent">Yayınlanacak event türü</typeparam>
        /// <param name="event">Yayınlanacak event</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem sonucu</returns>
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
    }
}
