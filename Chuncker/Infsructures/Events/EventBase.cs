using Chuncker.Interfaces;

namespace Chuncker.Infsructures.Events
{
    /// <summary>
    /// Tüm eventler için temel sınıf
    /// </summary>
    public abstract class EventBase : IEvent
    {
        protected EventBase()
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            CorrelationId = Guid.NewGuid(); // Varsayılan olarak yeni bir correlationId oluştur
        }

        protected EventBase(Guid correlationId)
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            CorrelationId = correlationId;
        }

        /// <summary>
        /// Event'in benzersiz kimliği
        /// </summary>
        public Guid EventId { get; private set; }

        /// <summary>
        /// Event'in türü
        /// </summary>
        public abstract string EventType { get; }

        /// <summary>
        /// Event'in oluşturulma zamanı
        /// </summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// İşlem izleme kimliği
        /// </summary>
        public Guid CorrelationId { get; set; }
    }
}
