using Chuncker.Applications.Events;
using Chuncker.Infsructures.Events;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.EventHandlers
{
    /// <summary>
    /// ChunkStoredEvent için event handler sınıfı
    /// </summary>
    public class ChunkStoredEventHandler : IEventHandler<ChunkStoredEvent>
    {
        private readonly ILogger<ChunkStoredEventHandler> _logger;

        /// <summary>
        /// ChunkStoredEventHandler constructor
        /// </summary>
        public ChunkStoredEventHandler(ILogger<ChunkStoredEventHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Event'i işler
        /// </summary>
        public Task HandleAsync(ChunkStoredEvent @event, System.Threading.CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Chunk saklandı: {ChunkId}, Dosya: {FileId}, Sıra: {SequenceNumber}, Storage Provider: {ProviderId}, Boyut: {ChunkSize} bytes, CorrelationId: {CorrelationId}",
                @event.ChunkId, @event.FileId, @event.SequenceNumber, @event.StorageProviderId, @event.ChunkSize, @event.CorrelationId);
            
            return Task.CompletedTask;
        }
    }
}
