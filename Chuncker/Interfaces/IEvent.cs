namespace Chuncker.Interfaces
{
    /// <summary>
    /// Temel event arayüzü
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// Event'in benzersiz kimliği
        /// </summary>
        Guid EventId { get; }

        /// <summary>
        /// Event'in türü
        /// </summary>
        string EventType { get; }

        /// <summary>
        /// Event'in oluşturulma zamanı
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// İşlem izleme kimliği
        /// </summary>
        Guid CorrelationId { get; }
    }
}
