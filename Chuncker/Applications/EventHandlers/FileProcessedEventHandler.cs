using Chuncker.Applications.Events;
using Chuncker.Infsructures.Events;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;

namespace Chuncker.Applications.EventHandlers
{
    /// <summary>
    /// FileProcessedEvent için event handler sınıfı
    /// </summary>
    public class FileProcessedEventHandler : IEventHandler<FileProcessedEvent>
    {
        private readonly ILogger<FileProcessedEventHandler> _logger;
        private readonly IFileMetadataService _fileMetadataService;

        /// <summary>
        /// FileProcessedEventHandler constructor
        /// </summary>
        public FileProcessedEventHandler(
            ILogger<FileProcessedEventHandler> logger,
            IFileMetadataService fileMetadataService)
        {
            _logger = logger;
            _fileMetadataService = fileMetadataService;
        }

        /// <summary>
        /// Event'i işler ve dosya metadata'sını günceller
        /// </summary>
        public async Task HandleAsync(FileProcessedEvent @event, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Dosya işlendi: {FileId}, {FileName}, Boyut: {FileSize} bytes, Parça sayısı: {ChunkCount}, CorrelationId: {CorrelationId}",
                    @event.FileId, @event.FileName, @event.FileSize, @event.ChunkCount, @event.CorrelationId);
                
                // Dosya metadatasını güncelle
                var fileMetadata = await _fileMetadataService.GetByIdAsync(@event.FileId, @event.CorrelationId, cancellationToken);
                
                if (fileMetadata != null)
                {
                    // Chunk sayısını güncelle
                    fileMetadata.ChunkCount = @event.ChunkCount;
                    fileMetadata.Status = Models.FileStatus.Completed;
                    fileMetadata.UpdatedAt = DateTime.UtcNow;
                    
                    // Dosya metadatasını kaydet
                    await _fileMetadataService.UpdateAsync(fileMetadata, @event.CorrelationId, cancellationToken);
                    
                    _logger.LogInformation(
                        "Dosya metadatası güncellendi: {FileId}, ChunkCount: {ChunkCount}, CorrelationId: {CorrelationId}",
                        @event.FileId, @event.ChunkCount, @event.CorrelationId);
                }
                else
                {
                    _logger.LogWarning(
                        "Dosya metadatası bulunamadı: {FileId}, CorrelationId: {CorrelationId}",
                        @event.FileId, @event.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Dosya metadatası güncellenirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}",
                    @event.FileId, @event.CorrelationId);
            }
        }
    }
}
