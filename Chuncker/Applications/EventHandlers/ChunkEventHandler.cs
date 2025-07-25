using Chuncker.Applications.Events;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.EventHandlers
{
    /// <summary>
    /// Generic chunk operations için event handler
    /// Chunk lifecycle'ındaki tüm olayları handle eder
    /// </summary>
    public class ChunkEventHandler : IEventHandler<ChunkStoredEvent>
    {
        private readonly ILogger<ChunkEventHandler> _logger;
        private readonly IChunkMetadataService _chunkMetadataService;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly IEventPublisher _eventPublisher;

        /// <summary>
        /// ChunkEventHandler constructor
        /// </summary>
        public ChunkEventHandler(
            ILogger<ChunkEventHandler> logger,
            IChunkMetadataService chunkMetadataService,
            IFileMetadataService fileMetadataService,
            IEventPublisher eventPublisher)
        {
            _logger = logger;
            _chunkMetadataService = chunkMetadataService;
            _fileMetadataService = fileMetadataService;
            _eventPublisher = eventPublisher;
        }

        /// <summary>
        /// ChunkStoredEvent'i işler ve chunk lifecycle management yapar
        /// </summary>
        public async Task HandleAsync(ChunkStoredEvent @event, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Chunk lifecycle management başlatılıyor: ChunkId: {ChunkId}, FileId: {FileId}, Sequence: {SequenceNumber}, CorrelationId: {CorrelationId}",
                    @event.ChunkId, @event.FileId, @event.SequenceNumber, @event.CorrelationId);

                // 1. Chunk durumunu güncelle
                await UpdateChunkLifecycleStatus(@event, cancellationToken);

                // 2. Dosyanın tüm chunk'larının tamamlanıp tamamlanmadığını kontrol et
                await CheckFileCompletionStatus(@event, cancellationToken);

                // 3. Chunk sequence validation
                await ValidateChunkSequence(@event, cancellationToken);

                // 4. Chunk metadata istatistiklerini güncelle
                await UpdateChunkStatistics(@event, cancellationToken);

                _logger.LogInformation(
                    "Chunk lifecycle management tamamlandı: ChunkId: {ChunkId}, FileId: {FileId}, CorrelationId: {CorrelationId}",
                    @event.ChunkId, @event.FileId, @event.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Chunk lifecycle management sırasında hata oluştu: ChunkId: {ChunkId}, FileId: {FileId}, CorrelationId: {CorrelationId}",
                    @event.ChunkId, @event.FileId, @event.CorrelationId);
            }
        }

        /// <summary>
        /// Chunk lifecycle durumunu günceller
        /// </summary>
        private async Task UpdateChunkLifecycleStatus(ChunkStoredEvent chunkEvent, CancellationToken cancellationToken)
        {
            try
            {
                var chunkMetadata = await _chunkMetadataService.GetByIdAsync(chunkEvent.ChunkId, chunkEvent.CorrelationId, cancellationToken);
                
                if (chunkMetadata != null)
                {
                    // Chunk durumunu 'Stored' olarak işaretle
                    chunkMetadata.Status = "Stored";
                    chunkMetadata.StorageTimestamp = DateTime.UtcNow;
                    chunkMetadata.LastAccessTime = DateTime.UtcNow;

                    await _chunkMetadataService.UpdateAsync(chunkMetadata, chunkEvent.CorrelationId, cancellationToken);
                    
                    _logger.LogDebug(
                        "Chunk lifecycle status güncellendi: ChunkId: {ChunkId}, Status: Stored",
                        chunkEvent.ChunkId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chunk lifecycle status güncellenirken hata oluştu: ChunkId: {ChunkId}",
                    chunkEvent.ChunkId);
            }
        }

        /// <summary>
        /// Dosyanın tüm chunk'larının tamamlanıp tamamlanmadığını kontrol eder
        /// </summary>
        private async Task CheckFileCompletionStatus(ChunkStoredEvent chunkEvent, CancellationToken cancellationToken)
        {
            try
            {
                // Chunk'ın FileId değerinden önce dosya ID'sini alma
                // Normalde FileId değerinin doğru olması gerekir ama hata durumunda
                // Chunk ID'sinden dosya ID'sini çıkarabiliriz
                string fileId = chunkEvent.FileId;
                
                // Eğer chunk ID'si "_" içeriyorsa ve FileId null ise, chunk ID'den FileId'yi çıkar
                if (string.IsNullOrEmpty(fileId) && chunkEvent.ChunkId.Contains("_"))
                {
                    fileId = chunkEvent.ChunkId.Split('_')[0];
                    _logger.LogInformation(
                        "FileId ChunkId'den çıkarıldı: ChunkId: {ChunkId}, FileId: {FileId}",
                        chunkEvent.ChunkId, fileId);
                }
                
                // Dosyanın metadata'sını al
                var fileMetadata = await _fileMetadataService.GetByIdAsync(fileId, chunkEvent.CorrelationId, cancellationToken);
                
                if (fileMetadata == null)
                {
                    _logger.LogWarning(
                        "File metadata bulunamadı chunk completion check için: FileId: {FileId}",
                        fileId);
                    return;
                }

                // Dosyanın toplam chunk sayısını al
                var allChunks = await _chunkMetadataService.GetChunksByFileIdAsync(fileId, chunkEvent.CorrelationId, cancellationToken);
                var storedChunks = allChunks?.Where(c => c.Status == "Stored").ToList();

                if (storedChunks != null && (fileMetadata.ChunkCount == 0 || storedChunks.Count >= fileMetadata.ChunkCount))
                {
                    // Gerçek chunk sayısını güncelle
                    fileMetadata.ChunkCount = storedChunks.Count;
                    await _fileMetadataService.UpdateAsync(fileMetadata, chunkEvent.CorrelationId, cancellationToken);
                    
                    _logger.LogInformation(
                        "Dosyanın tüm chunk'ları tamamlandı: FileId: {FileId}, TotalChunks: {TotalChunks}, StoredChunks: {StoredChunks}",
                        fileId, fileMetadata.ChunkCount, storedChunks.Count);

                    // File completed event publish et
                    await _eventPublisher.PublishAsync(new FileProcessedEvent
                    {
                        FileId = fileMetadata.Id,
                        FileName = fileMetadata.FileName,
                        FileSize = fileMetadata.FileSize,
                        ChunkCount = fileMetadata.ChunkCount,
                        CorrelationId = chunkEvent.CorrelationId,
                        Checksum = fileMetadata.Checksum
                    }, cancellationToken);
                }
                else
                {
                    _logger.LogDebug(
                        "Dosyanın chunk'ları henüz tamamlanmadı: FileId: {FileId}, ExpectedChunks: {ExpectedChunks}, StoredChunks: {StoredChunks}",
                        chunkEvent.FileId, fileMetadata.ChunkCount, storedChunks?.Count ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "File completion status kontrolü sırasında hata oluştu: FileId: {FileId}",
                    chunkEvent.FileId);
            }
        }

        /// <summary>
        /// Chunk sequence'ının doğruluğunu kontrol eder
        /// </summary>
        private async Task ValidateChunkSequence(ChunkStoredEvent chunkEvent, CancellationToken cancellationToken)
        {
            try
            {
                // Aynı dosyanın chunk'larını sequence number'a göre sırala
                var fileChunks = await _chunkMetadataService.GetChunksByFileIdAsync(chunkEvent.FileId, chunkEvent.CorrelationId, cancellationToken);
                
                if (fileChunks != null && fileChunks.Any())
                {
                    var sortedChunks = fileChunks.OrderBy(c => c.SequenceNumber).ToList();
                    
                    // Sequence number'larda gap var mı kontrol et
                    for (int i = 0; i < sortedChunks.Count; i++)
                    {
                        if (sortedChunks[i].SequenceNumber != i)
                        {
                            _logger.LogWarning(
                                "Chunk sequence gap tespit edildi: FileId: {FileId}, Expected: {Expected}, Actual: {Actual}, ChunkId: {ChunkId}",
                                chunkEvent.FileId, i, sortedChunks[i].SequenceNumber, sortedChunks[i].Id);
                        }
                    }

                    // Duplicate sequence number var mı kontrol et
                    var duplicateSequences = sortedChunks
                        .GroupBy(c => c.SequenceNumber)
                        .Where(g => g.Count() > 1)
                        .ToList();

                    if (duplicateSequences.Any())
                    {
                        foreach (var duplicate in duplicateSequences)
                        {
                            _logger.LogWarning(
                                "Duplicate chunk sequence tespit edildi: FileId: {FileId}, SequenceNumber: {SequenceNumber}, ChunkCount: {ChunkCount}",
                                chunkEvent.FileId, duplicate.Key, duplicate.Count());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chunk sequence validation sırasında hata oluştu: ChunkId: {ChunkId}, FileId: {FileId}",
                    chunkEvent.ChunkId, chunkEvent.FileId);
            }
        }

        /// <summary>
        /// Chunk istatistiklerini günceller
        /// </summary>
        private async Task UpdateChunkStatistics(ChunkStoredEvent chunkEvent, CancellationToken cancellationToken)
        {
            try
            {
                // Chunk ID'sinden fileId çıkar
                string fileId = chunkEvent.FileId;
                if (string.IsNullOrEmpty(fileId) && chunkEvent.ChunkId.Contains("_"))
                {
                    fileId = chunkEvent.ChunkId.Split('_')[0];
                }

                // Dosya için chunk istatistiklerini hesapla
                var fileChunks = await _chunkMetadataService.GetChunksByFileIdAsync(fileId, chunkEvent.CorrelationId, cancellationToken);
                
                if (fileChunks != null && fileChunks.Any())
                {
                    var totalOriginalSize = fileChunks.Sum(c => c.Size);
                    var totalCompressedSize = fileChunks.Sum(c => c.CompressedSize);
                    var compressionRatio = totalOriginalSize > 0 ? (double)totalCompressedSize / totalOriginalSize * 100 : 100.0;
                    var storedChunkCount = fileChunks.Count(c => c.Status == "Stored");

                    _logger.LogInformation(
                        "File chunk istatistikleri: FileId: {FileId}, TotalChunks: {TotalChunks}, StoredChunks: {StoredChunks}, " +
                        "OriginalSize: {OriginalSize} bytes, CompressedSize: {CompressedSize} bytes, CompressionRatio: {CompressionRatio:F2} %",
                        fileId, fileChunks.Count(), storedChunkCount, totalOriginalSize, totalCompressedSize, compressionRatio);

                    // Güncel chunk istatistiklerini file metadata'ya kaydet
                    var fileMetadata = await _fileMetadataService.GetByIdAsync(fileId, chunkEvent.CorrelationId, cancellationToken);
                    if (fileMetadata != null)
                    {
                        fileMetadata.ChunkCount = fileChunks.Count();
                        fileMetadata.UpdatedAt = DateTime.UtcNow;
                        
                        await _fileMetadataService.UpdateAsync(fileMetadata, chunkEvent.CorrelationId, cancellationToken);
                        _logger.LogInformation("Dosya metadata'sı güncellendi: FileId: {FileId}, ChunkCount: {ChunkCount}", 
                            fileId, fileMetadata.ChunkCount);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chunk statistics update sırasında hata oluştu: FileId: {FileId}",
                    chunkEvent.FileId);
            }
        }
    }
}