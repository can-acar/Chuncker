using Chuncker.Applications.Events;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.EventHandlers
{
    /// <summary>
    /// ChunkStoredEvent'ler için özel upload işlemleri yapan event handler
    /// Bu handler chunk'ların storage'a yüklenmesi sonrası ek işlemler yapar
    /// </summary>
    public class UploadToChunkStorageHandler : IEventHandler<ChunkStoredEvent>
    {
        private readonly ILogger<UploadToChunkStorageHandler> _logger;
        private readonly IChunkMetadataService _chunkMetadataService;
        private readonly IStorageProvider _primaryStorageProvider;

        /// <summary>
        /// UploadToChunkStorageHandler constructor
        /// </summary>
        public UploadToChunkStorageHandler(
            ILogger<UploadToChunkStorageHandler> logger,
            IChunkMetadataService chunkMetadataService,
            IEnumerable<IStorageProvider> storageProviders)
        {
            _logger = logger;
            _chunkMetadataService = chunkMetadataService;
            
            // Primary storage provider'ı belirle (ilk provider'ı kullan)
            _primaryStorageProvider = storageProviders.FirstOrDefault() 
                ?? throw new ArgumentException("En az bir storage provider gereklidir.");
        }

        /// <summary>
        /// ChunkStoredEvent'i işler ve ek upload işlemleri yapar
        /// </summary>
        public async Task HandleAsync(ChunkStoredEvent @event, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Chunk upload post-processing başlatılıyor: ChunkId: {ChunkId}, FileId: {FileId}, StorageProvider: {ProviderId}, CorrelationId: {CorrelationId}",
                    @event.ChunkId, @event.FileId, @event.StorageProviderId, @event.CorrelationId);

                // 1. Chunk metadata'sında storage path'i güncelle
                await UpdateChunkStorageInfo(@event, cancellationToken);

                // 2. Chunk boyut optimizasyonu kontrolü
                await AnalyzeChunkCompressionEfficiency(@event, cancellationToken);

                // 3. Chunk yedekleme stratejisi (eğer critical dosya ise)
                await ProcessChunkBackupStrategy(@event, cancellationToken);

                // 4. Storage provider sağlık kontrolü
                await ValidateStorageProviderHealth(@event, cancellationToken);

                _logger.LogInformation(
                    "Chunk upload post-processing tamamlandı: ChunkId: {ChunkId}, FileId: {FileId}, CorrelationId: {CorrelationId}",
                    @event.ChunkId, @event.FileId, @event.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Chunk upload post-processing sırasında hata oluştu: ChunkId: {ChunkId}, FileId: {FileId}, CorrelationId: {CorrelationId}",
                    @event.ChunkId, @event.FileId, @event.CorrelationId);
                
                // Bu handler'da hata olursa chunk upload'u etkilenmemeli
                // Sadece ek işlemler başarısız olur
            }
        }

        /// <summary>
        /// Chunk metadata'sını storage bilgileri ile günceller
        /// </summary>
        private async Task UpdateChunkStorageInfo(ChunkStoredEvent chunkEvent, CancellationToken cancellationToken)
        {
            try
            {
                var chunkMetadata = await _chunkMetadataService.GetByIdAsync(chunkEvent.ChunkId, chunkEvent.CorrelationId, cancellationToken);
                
                if (chunkMetadata != null)
                {
                    // Storage timestamp'ini güncelle
                    chunkMetadata.StorageTimestamp = DateTime.UtcNow;
                    
                    // Storage provider bilgilerini doğrula
                    if (chunkMetadata.StorageProviderId != chunkEvent.StorageProviderId)
                    {
                        _logger.LogWarning(
                            "Chunk metadata'daki storage provider ID uyumsuz: Expected: {Expected}, Actual: {Actual}, ChunkId: {ChunkId}",
                            chunkMetadata.StorageProviderId, chunkEvent.StorageProviderId, chunkEvent.ChunkId);
                    }

                    await _chunkMetadataService.UpdateAsync(chunkMetadata, chunkEvent.CorrelationId, cancellationToken);
                    
                    _logger.LogDebug(
                        "Chunk storage info güncellendi: ChunkId: {ChunkId}, StorageProvider: {ProviderId}",
                        chunkEvent.ChunkId, chunkEvent.StorageProviderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chunk storage info güncellenirken hata oluştu: ChunkId: {ChunkId}",
                    chunkEvent.ChunkId);
            }
        }

        /// <summary>
        /// Chunk sıkıştırma verimliliğini analiz eder
        /// </summary>
        private async Task AnalyzeChunkCompressionEfficiency(ChunkStoredEvent chunkEvent, CancellationToken cancellationToken)
        {
            try
            {
                if (chunkEvent.ChunkSize > 0 && chunkEvent.CompressedSize > 0)
                {
                    var compressionRatio = (double)chunkEvent.CompressedSize / chunkEvent.ChunkSize;
                    
                    _logger.LogDebug(
                        "Chunk compression analizi: ChunkId: {ChunkId}, OriginalSize: {OriginalSize}, CompressedSize: {CompressedSize}, Ratio: {Ratio:P2}",
                        chunkEvent.ChunkId, chunkEvent.ChunkSize, chunkEvent.CompressedSize, compressionRatio);

                    // Eğer sıkıştırma çok düşükse (>95%) uyarı ver
                    if (compressionRatio > 0.95)
                    {
                        _logger.LogInformation(
                            "Düşük compression ratio tespit edildi: ChunkId: {ChunkId}, Ratio: {Ratio:P2} - Dosya türü sıkıştırmaya uygun olmayabilir",
                            chunkEvent.ChunkId, compressionRatio);
                    }
                    // Eğer sıkıştırma çok iyiyse (<50%) bilgi ver
                    else if (compressionRatio < 0.50)
                    {
                        _logger.LogInformation(
                            "Yüksek compression ratio: ChunkId: {ChunkId}, Ratio: {Ratio:P2} - Excellent compression achieved",
                            chunkEvent.ChunkId, compressionRatio);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chunk compression analizi sırasında hata oluştu: ChunkId: {ChunkId}",
                    chunkEvent.ChunkId);
            }
        }

        /// <summary>
        /// Critical dosyalar için chunk yedekleme stratejisini işler
        /// </summary>
        private async Task ProcessChunkBackupStrategy(ChunkStoredEvent chunkEvent, CancellationToken cancellationToken)
        {
            try
            {
                // Bu örnek implementasyonda sadece logging yapıyoruz
                // Gerçek implementasyonda chunk'ı başka storage provider'lara da kopyalayabilir
                
                _logger.LogDebug(
                    "Chunk backup strategy kontrolü: ChunkId: {ChunkId}, FileId: {FileId}",
                    chunkEvent.ChunkId, chunkEvent.FileId);

                // Future: Burada chunk'ın backup edilip edilmeyeceğine karar verilebilir
                // Örneğin: dosya boyutu, dosya türü, önem derecesi gibi kriterlere göre
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chunk backup strategy işlemi sırasında hata oluştu: ChunkId: {ChunkId}",
                    chunkEvent.ChunkId);
            }
        }

        /// <summary>
        /// Storage provider sağlık durumunu kontrol eder
        /// </summary>
        private async Task ValidateStorageProviderHealth(ChunkStoredEvent chunkEvent, CancellationToken cancellationToken)
        {
            try
            {
                // Storage provider'ın sağlık durumunu kontrol et
                if (_primaryStorageProvider.ProviderId == chunkEvent.StorageProviderId)
                {
                    // Basit bir health check - chunk'ın gerçekten okunabilir olup olmadığını kontrol et
                    try
                    {
                        await using var testStream = await _primaryStorageProvider.ReadChunkAsync(
                            chunkEvent.ChunkId, null, chunkEvent.CorrelationId, cancellationToken);
                        
                        if (testStream == null || !testStream.CanRead)
                        {
                            _logger.LogWarning(
                                "Storage provider health check başarısız: ChunkId: {ChunkId}, ProviderId: {ProviderId}",
                                chunkEvent.ChunkId, chunkEvent.StorageProviderId);
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Storage provider health check başarılı: ChunkId: {ChunkId}, ProviderId: {ProviderId}",
                                chunkEvent.ChunkId, chunkEvent.StorageProviderId);
                        }
                    }
                    catch (Exception healthEx)
                    {
                        _logger.LogWarning(healthEx,
                            "Storage provider health check sırasında hata: ChunkId: {ChunkId}, ProviderId: {ProviderId}",
                            chunkEvent.ChunkId, chunkEvent.StorageProviderId);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Storage provider health validation sırasında hata oluştu: ChunkId: {ChunkId}",
                    chunkEvent.ChunkId);
            }
        }
    }
}