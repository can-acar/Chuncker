using Chuncker.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Applications.Events;
using Chuncker.Infsructures.Events;
using Chuncker.Interfaces;

namespace Chuncker.Services
{
    /// <summary>
    /// Dosya parçalama (chunking) işlemlerini yöneten sınıf (Optimize edilmiş)
    /// </summary>
    public class ChunkManager : IChunkManager
    {
        private readonly ILogger<ChunkManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IEventPublisher _eventPublisher;
        private readonly IChunkMetadataRepository _chunkRepository;
        private readonly IFileMetadataRepository _fileRepository;
        private readonly IEnumerable<IStorageProvider> _storageProviders;
        private readonly ChunkSettings _chunkSettings;
        private readonly ArrayPool<byte> _arrayPool;
        private readonly SemaphoreSlim _concurrencyLimiter;
        
        public ChunkManager(
            ILogger<ChunkManager> logger,
            ILoggerFactory loggerFactory,
            IEventPublisher eventPublisher,
            IChunkMetadataRepository chunkRepository,
            IFileMetadataRepository fileRepository,
            IEnumerable<IStorageProvider> storageProviders,
            IConfiguration configuration)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _eventPublisher = eventPublisher;
            _chunkRepository = chunkRepository;
            _fileRepository = fileRepository;
            _storageProviders = storageProviders.ToList();
            
            // Yapılandırmadan chunk ayarlarını oku
            _chunkSettings = configuration.GetSection("ChunkSettings").Get<ChunkSettings>() 
                ?? new ChunkSettings();
            
            if (_storageProviders == null || !_storageProviders.Any())
            {
                throw new ArgumentException("En az bir storage provider sağlanmalıdır.");
            }
            
            // ArrayPool kullanarak bellek kullanımını optimize et
            _arrayPool = ArrayPool<byte>.Shared;
            
            // Eş zamanlı işlem sınırlaması için semaphore
            var maxConcurrency = configuration.GetValue("DistributionSettings:MaxParallelTasks", 4);
            _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        /// <summary>
        /// Bir dosyayı parçalara böler ve her parçayı depolama sağlayıcılarına dağıtır (Optimize edilmiş)
        /// </summary>
        public async Task<FileMetadata> SplitFileAsync(Stream fileStream, string fileId, string fileName,
            Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya parçalama işlemi başlatıldı: {FileName}, CorrelationId: {CorrelationId}", 
                fileName, correlationId);
            
            // Dosya boyutunu kontrol et
            if (fileStream.Length == 0)
            {
                throw new ArgumentException("Dosya boş olamaz.");
            }
            
            var fileSize = fileStream.Length;
            
            // Parça boyutunu hesapla
            var chunkSize = CalculateOptimalChunkSize(fileSize);
            _logger.LogInformation("Optimal parça boyutu hesaplandı: {ChunkSize} bytes, CorrelationId: {CorrelationId}", 
                chunkSize, correlationId);
            
            // Toplam parça sayısını hesapla
            var totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
            
            // Dosya metadatasını oluştur - Aynı ID'yi hem dosya hem de chunk'lar için kullanacağız
            var fileMetadata = new FileMetadata
            {
                Id = fileId,
                FileName = fileName,
                FileSize = fileSize,
                ContentType = Path.GetExtension(fileName).TrimStart('.'),
                CreatedAt = DateTime.UtcNow,
                ChunkCount = totalChunks, // Başlangıçta beklenen chunk sayısını ayarla
                CorrelationId = correlationId
            };
            
            _logger.LogInformation("Yeni dosya metaverisi ve fileId oluşturuldu: {FileName}, Id: {FileId}", fileName, fileId);
            
            // Dosya checksum'ını hesapla
            fileStream.Position = 0;
            fileMetadata.Checksum = await CalculateChecksumAsync(fileStream, cancellationToken);
            fileStream.Position = 0;
            
            // Dosyayı parçalara böl
            var chunks = new List<ChunkMetadata>();
            var sequenceNumber = 0;
            
            // Memory-mapped file processor kullanarak büyük dosyaları verimli işle
            using (var processor = new MemoryMappedFileProcessor(_loggerFactory.CreateLogger<MemoryMappedFileProcessor>()))
            {
                // Dosyayı memory-mapped file'a kopyala
                await using var mappedStream = await processor.CreateFromStreamAsync(fileStream, correlationId);
                
                // Round-robin provider seçimi için index
                var providerIndex = 0;
                
                // Storage provider cache'i
                var providerCache = _storageProviders.ToDictionary(p => p.ProviderId);
                
                // Paralel işleme için task koleksiyonu
                var tasks = new List<Task<ChunkMetadata>>();
                
                // Dosyayı parçalara böl
                while ((sequenceNumber * chunkSize) < fileSize)
                {
                    // İşlemi iptal et
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Bu parça için gerekli değerleri hesapla
                    // fileId'nin orijinal değerini kullanarak chunk ID oluştur
                    var chunkId = $"{fileId}_{sequenceNumber}";
                    var offset = sequenceNumber * chunkSize;
                    var currentChunkSize = Math.Min(chunkSize, fileSize - offset);
                    
                    _logger.LogInformation("Chunk ID oluşturuldu: {ChunkId} dosya için: {FileId}", chunkId, fileId);
                    
                    // Sıradaki provider'ı seç (round-robin)
                    var providerId = _storageProviders.ElementAt(providerIndex % _storageProviders.Count()).ProviderId;
                    providerIndex++;
                    
                    // Asenkron işleme için kapanma değişkenleri
                    var currentSequenceNumber = sequenceNumber;
                    var currentOffset = offset;
                    var currentChunkId = chunkId;
                    var chunkSizeForTask = currentChunkSize;
                    var currentProviderId = providerId;
                    
                    // Paralel işlem sayısını sınırla
                    await _concurrencyLimiter.WaitAsync(cancellationToken);
                    
                    // Her parçayı asenkron olarak işle
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            // Memory-mapped file'dan parçayı oku
                            using var chunkStream = processor.ReadChunk(currentOffset, chunkSizeForTask, correlationId);
                            
                            // Parça checksum'ını hesapla
                            var chunkChecksum = await CalculateChecksumAsync(chunkStream, cancellationToken);
                            chunkStream.Position = 0;
                            
                            // Parça metadata'sını oluştur
                            var chunkMetadata = new ChunkMetadata
                            {
                                Id = currentChunkId,
                                FileId = fileId,
                                SequenceNumber = currentSequenceNumber,
                                Size = chunkSizeForTask,
                                CompressedSize = chunkSizeForTask, // Başlangıçta sıkıştırılmamış
                                Checksum = chunkChecksum,
                                CreatedAt = DateTime.UtcNow,
                                IsCompressed = false,
                                CorrelationId = correlationId,
                                StorageProviderId = currentProviderId
                            };
                            
                            // İlgili storage provider'ı bul
                            if (!providerCache.TryGetValue(currentProviderId, out var provider))
                            {
                                throw new InvalidOperationException($"Provider bulunamadı: {currentProviderId}");
                            }
                            
                            // Sıkıştırma etkin ise
                            if (_chunkSettings.CompressionEnabled)
                            {
                                // ArrayPool'dan buffer al
                                byte[] compressBuffer = null;
                                try
                                {
                                    // Sıkıştırma için buffer oluştur (en kötü senaryoda orijinal boyut)
                                    compressBuffer = _arrayPool.Rent((int)chunkSizeForTask);
                                    
                                    // Verimli sıkıştırma
                                    using var compressedMs = new MemoryStream();
                                    using (var gzipStream = new GZipStream(compressedMs, GetCompressionLevel(), true))
                                    {
                                        await chunkStream.CopyToAsync(gzipStream, cancellationToken);
                                    }
                                    
                                    // Sıkıştırılmış boyutu güncelle
                                    compressedMs.Position = 0;
                                    chunkMetadata.CompressedSize = compressedMs.Length;
                                    chunkMetadata.IsCompressed = true;
                                    
                                    // Parçayı depolama sağlayıcısına yaz
                                    chunkMetadata.StoragePath = await provider.WriteChunkAsync(
                                        chunkId, compressedMs, correlationId, cancellationToken);
                                }
                                finally
                                {
                                    // Buffer'ı havuza geri döndür
                                    if (compressBuffer != null)
                                        _arrayPool.Return(compressBuffer);
                                }
                            }
                            else
                            {
                                // Parçayı sıkıştırmadan depola
                                chunkMetadata.StoragePath = await provider.WriteChunkAsync(
                                    chunkId, chunkStream, correlationId, cancellationToken);
                            }
                            
                            // Parça metadata'sını kaydet
                            await _chunkRepository.AddAsync(chunkMetadata, correlationId, cancellationToken);
                            
                            // Event gönder
                            await _eventPublisher.PublishAsync(
                                new ChunkStoredEvent(
                                    chunkMetadata.Id,
                                    chunkMetadata.FileId,
                                    chunkMetadata.SequenceNumber,
                                    chunkMetadata.Size,
                                    chunkMetadata.CompressedSize,
                                    chunkMetadata.Checksum,
                                    chunkMetadata.StorageProviderId,
                                    correlationId),
                                cancellationToken);
                            
                            return chunkMetadata;
                        }
                        finally
                        {
                            // Semaphore'u serbest bırak
                            _concurrencyLimiter.Release();
                        }
                    }, cancellationToken);
                    
                    tasks.Add(task);
                    sequenceNumber++;
                }
                
                // Tüm parça işleme görevlerini tamamla
                var chunkResults = await Task.WhenAll(tasks);
                chunks.AddRange(chunkResults);
            }
            
            // Dosya metadata'sına parçaları ekle ve sırala
            fileMetadata.Chunks = chunks.OrderBy(c => c.SequenceNumber).ToList();
            
            // Güncel chunk sayısını güncelle
            fileMetadata.ChunkCount = chunks.Count;
            
            // Event gönder
            var fileProcessedEvent = new FileProcessedEvent(
                fileId,
                fileMetadata.FileName,
                fileMetadata.FileSize,
                fileMetadata.Checksum,
                correlationId);
                
            // Chunk sayısını event'e ekle
            fileProcessedEvent.ChunkCount = chunks.Count;
            
            await _eventPublisher.PublishAsync(fileProcessedEvent, cancellationToken);
            
            _logger.LogInformation(
                "Dosya başarıyla parçalandı: {FileName}, FileId: {FileId}, {ChunkCount} parça, CorrelationId: {CorrelationId}", 
                fileName, fileId, chunks.Count, correlationId);
            
            return fileMetadata;
        }

        /// <summary>
        /// Parçaları birleştirerek orijinal dosyayı yeniden oluşturur (Optimize edilmiş)
        /// </summary>
        public async Task<bool> MergeChunksAsync(string fileId, Stream outputStream, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya parçalarını birleştirme işlemi başlatıldı: {FileId}, CorrelationId: {CorrelationId}", 
                fileId, correlationId);
            
            try
            {
                // Önce dosya ID'sinden parçaları getir
                var chunks = (await _chunkRepository.GetChunksByFileIdAsync(fileId, correlationId, cancellationToken))
                    .OrderBy(c => c.SequenceNumber)
                    .ToList();
                
                // Eğer parça bulunamadıysa, chunk ID pattern'ine göre arama yapalım
                if (chunks.Count == 0)
                {
                    // Tüm chunk'ları getir ve fileId ile başlayan ID'leri filtrele
                    var allChunks = await _chunkRepository.GetAllAsync(correlationId, cancellationToken);
                    var matchingChunks = allChunks
                        .Where(c => c.Id.StartsWith($"{fileId}_") || c.FileId == fileId)
                        .OrderBy(c => c.SequenceNumber)
                        .ToList();
                    
                    if (matchingChunks.Count > 0)
                    {
                        _logger.LogInformation("Alternatif pattern ile {Count} parça bulundu: {FileId}, CorrelationId: {CorrelationId}", 
                            matchingChunks.Count, fileId, correlationId);
                        chunks = matchingChunks;
                    }
                }
                
                if (chunks.Count == 0)
                {
                    _logger.LogError("Dosyaya ait parça bulunamadı: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                    return false;
                }
                
                _logger.LogInformation("Dosya için {ChunkCount} parça bulundu: {FileId}, CorrelationId: {CorrelationId}", 
                    chunks.Count, fileId, correlationId);
                
                // Dictionary ile provider'ları önbelleğe al
                var providerCache = _storageProviders.ToDictionary(p => p.ProviderId);
                
                // Parça decompression için buffer havuzu
                byte[] decompressionBuffer = null;
                int maxChunkSize = chunks.Max(c => (int)c.Size);
                
                try
                {
                    // Decompression için buffer oluştur
                    decompressionBuffer = _arrayPool.Rent(maxChunkSize);
                    
                    // Her parçayı sırayla işle
                    foreach (var chunk in chunks)
                    {
                        // İşlemi iptal et
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Parçanın depolama sağlayıcısını bul
                        if (!providerCache.TryGetValue(chunk.StorageProviderId, out var provider))
                        {
                            _logger.LogError("Parça için storage provider bulunamadı: {ChunkId}, ProviderId: {ProviderId}, CorrelationId: {CorrelationId}", 
                                chunk.Id, chunk.StorageProviderId, correlationId);
                            return false;
                        }
                        
                        // Parçayı oku
                        using var chunkStream = await provider.ReadChunkAsync(chunk.Id, chunk.StoragePath, correlationId, cancellationToken);
                        
                        // Parça sıkıştırılmış ise açılması gerekir
                        if (chunk.IsCompressed)
                        {
                            using var decompressedStream = new MemoryStream();
                            using (var gzipStream = new GZipStream(chunkStream, CompressionMode.Decompress))
                            {
                                await gzipStream.CopyToAsync(decompressedStream, cancellationToken);
                            }
                            
                            // Decompressed stream'i başa al ve çıktıya yaz
                            decompressedStream.Position = 0;
                            await decompressedStream.CopyToAsync(outputStream, cancellationToken);
                        }
                        else
                        {
                            // Sıkıştırılmamış parçayı doğrudan çıktıya yaz
                            await chunkStream.CopyToAsync(outputStream, cancellationToken);
                        }
                        
                        _logger.LogInformation(
                            "Parça başarıyla birleştirildi: {ChunkId}, Sıra: {SequenceNumber}, CorrelationId: {CorrelationId}", 
                            chunk.Id, chunk.SequenceNumber, correlationId);
                    }
                }
                finally
                {
                    // Buffer'ı havuza geri döndür
                    if (decompressionBuffer != null)
                        _arrayPool.Return(decompressionBuffer);
                }
                
                _logger.LogInformation("Dosya parçaları başarıyla birleştirildi: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya parçaları birleştirilirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Bir dosyaya ait tüm parçaları siler (Optimize edilmiş)
        /// </summary>
        public async Task<bool> DeleteChunksAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya parçalarını silme işlemi başlatıldı: {FileId}, CorrelationId: {CorrelationId}", 
                fileId, correlationId);
            
            try
            {
                // Dosyaya ait tüm parçaları getir
                var chunks = await _chunkRepository.GetChunksByFileIdAsync(fileId, correlationId, cancellationToken);
                
                if (!chunks.Any())
                {
                    _logger.LogWarning("Dosyaya ait parça bulunamadı: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                    return true; // Parça bulunamadıysa başarılı kabul et
                }
                
                _logger.LogInformation("Dosya için {ChunkCount} parça siliniyor: {FileId}, CorrelationId: {CorrelationId}", 
                    chunks.Count(), fileId, correlationId);
                
                // Dictionary ile provider'ları önbelleğe al
                var providerCache = _storageProviders.ToDictionary(p => p.ProviderId);
                
                // Parçaları provider'lara göre gruplandır
                var chunksByProvider = chunks.GroupBy(c => c.StorageProviderId);
                
                // Her provider için paralel işlem başlat
                var deleteTasks = new List<Task<bool>>();
                
                foreach (var providerGroup in chunksByProvider)
                {
                    var providerId = providerGroup.Key;
                    
                    // Provider'ı bul
                    if (!providerCache.TryGetValue(providerId, out var provider))
                    {
                        _logger.LogError("Parçalar için storage provider bulunamadı: ProviderId: {ProviderId}, CorrelationId: {CorrelationId}", 
                            providerId, correlationId);
                        continue;
                    }
                    
                    // Bu provider'daki tüm parçaları sil
                    var providerTask = Task.Run(async () =>
                    {
                        var success = true;
                        
                        foreach (var chunk in providerGroup)
                        {
                            // İşlemi iptal et
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // Parçayı sil
                            var chunkSuccess = await provider.DeleteChunkAsync(chunk.Id, chunk.StoragePath, correlationId, cancellationToken);
                            
                            if (!chunkSuccess)
                            {
                                _logger.LogWarning("Parça silinemedi: {ChunkId}, CorrelationId: {CorrelationId}", 
                                    chunk.Id, correlationId);
                                success = false;
                            }
                        }
                        
                        return success;
                    }, cancellationToken);
                    
                    deleteTasks.Add(providerTask);
                }
                
                // Tüm silme görevlerinin tamamlanmasını bekle
                var results = await Task.WhenAll(deleteTasks);
                var allSuccess = results.All(r => r);
                
                // Parça metadata'larını veritabanından sil
                await _chunkRepository.DeleteChunksByFileIdAsync(fileId, correlationId, cancellationToken);
                
                _logger.LogInformation("Dosya parçaları silme işlemi tamamlandı: {FileId}, Başarılı: {Success}, CorrelationId: {CorrelationId}", 
                    fileId, allSuccess, correlationId);
                
                return allSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya parçaları silinirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Optimal chunk boyutunu hesaplar (Optimize edilmiş)
        /// </summary>
        public long CalculateOptimalChunkSize(long fileSize)
        {
            if (fileSize <= 0)
            {
                return _chunkSettings.DefaultChunkSizeInBytes;
            }
            
            // Dosya boyutuna göre dinamik chunk boyutu hesapla
            // Küçük dosyalar için daha küçük chunk'lar, büyük dosyalar için daha büyük chunk'lar
            const long KB = 1024;
            const long MB = 1024 * KB;
            const long GB = 1024 * MB;
            
            if (fileSize < MB) // 1 MB'tan küçük
            {
                // Çok küçük dosyaları bölmemeye çalış
                return Math.Max(_chunkSettings.MinChunkSizeInBytes, fileSize);
            }
            else if (fileSize < 10 * MB) // 1-10 MB
            {
                return Math.Max(_chunkSettings.MinChunkSizeInBytes, Math.Min(MB, _chunkSettings.DefaultChunkSizeInBytes));
            }
            else if (fileSize < 100 * MB) // 10-100 MB
            {
                return Math.Max(2 * MB, Math.Min(_chunkSettings.DefaultChunkSizeInBytes, fileSize / 10));
            }
            else if (fileSize < GB) // 100 MB - 1 GB
            {
                return Math.Min(5 * MB, _chunkSettings.MaxChunkSizeInBytes);
            }
            else if (fileSize < 10 * GB) // 1-10 GB
            {
                return Math.Min(10 * MB, _chunkSettings.MaxChunkSizeInBytes);
            }
            else // 10 GB'tan büyük
            {
                return _chunkSettings.MaxChunkSizeInBytes;
            }
        }

        /// <summary>
        /// Bir dosyayı parçalara böler ve her parçayı depolama sağlayıcılarına dağıtır (belirtilen dosya ID'si ile).
        /// </summary>
        /// <param name="fileStream">Parçalanacak dosya akışı</param>
        /// <param name="fileId">Kullanılacak dosya kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="useExistingFileId">True ise fileId parameterini var olan dosya ID'si olarak kullan</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Güncellenen dosya metadata bilgisi</returns>
        public async Task<FileMetadata> SplitFileWithExistingIdAsync(Stream fileStream, string fileId, Guid correlationId, bool useExistingFileId = true, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya parçalama işlemi başlatıldı (var olan ID ile): {FileId}, CorrelationId: {CorrelationId}", 
                fileId, correlationId);
            
            // Dosya boyutunu kontrol et
            if (fileStream.Length == 0)
            {
                throw new ArgumentException("Dosya boş olamaz.");
            }
            
            // Dosya bilgilerini getir veya oluştur
            FileMetadata fileMetadata;
            
            if (useExistingFileId)
            {
                // Var olan dosya metadata'sını getir
                fileMetadata = await _fileRepository.GetByIdAsync(fileId, correlationId, cancellationToken);
                
                if (fileMetadata == null)
                {
                    throw new ArgumentException($"Belirtilen ID ile dosya bulunamadı: {fileId}");
                }
                
                // Var olan parçaları sil
                await DeleteChunksAsync(fileId, correlationId, cancellationToken);
            }
            else
            {
                // Yeni dosya metadata'sı oluştur
                fileMetadata = new FileMetadata
                {
                    Id = fileId,
                    FileName = "unknown.file", // Daha sonra güncellenecek
                    FileSize = fileStream.Length,
                    ContentType = "application/octet-stream", // Varsayılan içerik türü
                    CreatedAt = DateTime.UtcNow,
                    CorrelationId = correlationId
                };
            }
            
            // Dosya boyutunu ve güncelleme tarihini güncelle
            fileMetadata.FileSize = fileStream.Length;
            fileMetadata.UpdatedAt = DateTime.UtcNow;
            
            // Dosya checksum'ını hesapla
            fileStream.Position = 0;
            fileMetadata.Checksum = await CalculateChecksumAsync(fileStream, cancellationToken);
            fileStream.Position = 0;
            
            // Parça boyutunu hesapla
            var chunkSize = CalculateOptimalChunkSize(fileMetadata.FileSize);
            _logger.LogInformation("Optimal parça boyutu hesaplandı: {ChunkSize} bytes, CorrelationId: {CorrelationId}", 
                chunkSize, correlationId);
            
            // Dosyayı parçalara böl
            var chunks = new List<ChunkMetadata>();
            var sequenceNumber = 0;
            
            // Memory-mapped file processor kullanarak büyük dosyaları verimli işle
            using (var processor = new MemoryMappedFileProcessor(_loggerFactory.CreateLogger<MemoryMappedFileProcessor>()))
            {
                // Dosyayı memory-mapped file'a kopyala
                using var mappedStream = await processor.CreateFromStreamAsync(fileStream, correlationId);
                
                // Round-robin provider seçimi için index
                var providerIndex = 0;
                
                // Storage provider cache'i
                var providerCache = _storageProviders.ToDictionary(p => p.ProviderId);
                
                // Paralel işleme için task koleksiyonu
                var tasks = new List<Task<ChunkMetadata>>();
                
                // Dosyayı parçalara böl
                while ((sequenceNumber * chunkSize) < fileMetadata.FileSize)
                {
                    // İşlemi iptal et
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Bu parça için gerekli değerleri hesapla
                    var chunkId = $"{fileId}_{sequenceNumber}";
                    var offset = sequenceNumber * chunkSize;
                    var currentChunkSize = Math.Min(chunkSize, fileMetadata.FileSize - offset);
                    
                    // Sıradaki provider'ı seç (round-robin)
                    var providerId = _storageProviders.ElementAt(providerIndex % _storageProviders.Count()).ProviderId;
                    providerIndex++;
                    
                    // Asenkron işleme için kapanma değişkenleri
                    var currentSequenceNumber = sequenceNumber;
                    var currentOffset = offset;
                    var currentChunkId = chunkId;
                    var chunkSizeForTask = currentChunkSize;
                    var currentProviderId = providerId;
                    
                    // Paralel işlem sayısını sınırla
                    await _concurrencyLimiter.WaitAsync(cancellationToken);
                    
                    // Her parçayı asenkron olarak işle
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            // Memory-mapped file'dan parçayı oku
                            using var chunkStream = processor.ReadChunk(currentOffset, chunkSizeForTask, correlationId);
                            
                            // Parça checksum'ını hesapla
                            var chunkChecksum = await CalculateChecksumAsync(chunkStream, cancellationToken);
                            chunkStream.Position = 0;
                            
                            // Parça metadata'sını oluştur
                            var chunkMetadata = new ChunkMetadata
                            {
                                Id = currentChunkId,
                                FileId = fileId,
                                SequenceNumber = currentSequenceNumber,
                                Size = chunkSizeForTask,
                                CompressedSize = chunkSizeForTask, // Başlangıçta sıkıştırılmamış
                                Checksum = chunkChecksum,
                                CreatedAt = DateTime.UtcNow,
                                IsCompressed = false,
                                CorrelationId = correlationId,
                                StorageProviderId = currentProviderId
                            };
                            
                            // İlgili storage provider'ı bul
                            if (!providerCache.TryGetValue(currentProviderId, out var provider))
                            {
                                throw new InvalidOperationException($"Provider bulunamadı: {currentProviderId}");
                            }
                            
                            // Sıkıştırma etkin ise
                            if (_chunkSettings.CompressionEnabled)
                            {
                                // ArrayPool'dan buffer al
                                byte[] compressBuffer = null;
                                try
                                {
                                    // Sıkıştırma için buffer oluştur (en kötü senaryoda orijinal boyut)
                                    compressBuffer = _arrayPool.Rent((int)chunkSizeForTask);
                                    
                                    // Verimli sıkıştırma
                                    using var compressedMs = new MemoryStream();
                                    using (var gzipStream = new GZipStream(compressedMs, GetCompressionLevel(), true))
                                    {
                                        await chunkStream.CopyToAsync(gzipStream, cancellationToken);
                                    }
                                    
                                    // Sıkıştırılmış boyutu güncelle
                                    compressedMs.Position = 0;
                                    chunkMetadata.CompressedSize = compressedMs.Length;
                                    chunkMetadata.IsCompressed = true;
                                    
                                    // Parçayı depolama sağlayıcısına yaz
                                    chunkMetadata.StoragePath = await provider.WriteChunkAsync(
                                        chunkId, compressedMs, correlationId, cancellationToken);
                                }
                                finally
                                {
                                    // Buffer'ı havuza geri döndür
                                    if (compressBuffer != null)
                                        _arrayPool.Return(compressBuffer);
                                }
                            }
                            else
                            {
                                // Parçayı sıkıştırmadan depola
                                chunkMetadata.StoragePath = await provider.WriteChunkAsync(
                                    chunkId, chunkStream, correlationId, cancellationToken);
                            }
                            
                            // Parça metadata'sını kaydet
                            await _chunkRepository.AddAsync(chunkMetadata, correlationId, cancellationToken);
                            
                            // Event gönder
                            await _eventPublisher.PublishAsync(
                                new ChunkStoredEvent(
                                    chunkMetadata.Id,
                                    chunkMetadata.FileId,
                                    chunkMetadata.SequenceNumber,
                                    chunkMetadata.Size,
                                    chunkMetadata.CompressedSize,
                                    chunkMetadata.Checksum,
                                    chunkMetadata.StorageProviderId,
                                    correlationId),
                                cancellationToken);
                            
                            return chunkMetadata;
                        }
                        finally
                        {
                            // Semaphore'u serbest bırak
                            _concurrencyLimiter.Release();
                        }
                    }, cancellationToken);
                    
                    tasks.Add(task);
                    sequenceNumber++;
                }
                
                // Tüm parça işleme görevlerini tamamla
                var chunkResults = await Task.WhenAll(tasks);
                chunks.AddRange(chunkResults);
            }
            
            // Dosya metadata'sına parçaları ekle ve sırala
            fileMetadata.Chunks = chunks.OrderBy(c => c.SequenceNumber).ToList();
            fileMetadata.ChunkCount = chunks.Count;
            
            // Dosya metadata'sını kaydet veya güncelle
            if (useExistingFileId)
            {
                await _fileRepository.UpdateAsync(fileMetadata, correlationId, cancellationToken);
            }
            else
            {
                await _fileRepository.AddAsync(fileMetadata, correlationId, cancellationToken);
            }
            
            // Event gönder
            await _eventPublisher.PublishAsync(
                new FileProcessedEvent(
                    fileMetadata.Id,
                    fileMetadata.FileName,
                    fileMetadata.FileSize,
                    fileMetadata.Checksum,
                    correlationId),
                cancellationToken);
            
            _logger.LogInformation(
                "Dosya başarıyla parçalandı (var olan ID ile): {FileId}, {ChunkCount} parça, CorrelationId: {CorrelationId}", 
                fileId, chunks.Count, correlationId);
            
            return fileMetadata;
        }

        /// <summary>
        /// Bir chunk'ı siler (optimize edilmiş)
        /// </summary>
        /// <param name="chunkId">Chunk kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Silme işleminin başarılı olup olmadığını gösteren değer</returns>
        public async Task<bool> DeleteChunkAsync(string chunkId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk silme işlemi başlatıldı: {ChunkId}, CorrelationId: {CorrelationId}", 
                chunkId, correlationId);
            
            try
            {
                // Chunk metadata'sını getir
                var chunk = await _chunkRepository.GetByIdAsync(chunkId, correlationId, cancellationToken);
                
                if (chunk == null)
                {
                    _logger.LogWarning("Silinecek chunk bulunamadı: {ChunkId}, CorrelationId: {CorrelationId}", 
                        chunkId, correlationId);
                    return false;
                }
                
                // Storage provider'ı bul
                var provider = _storageProviders.FirstOrDefault(p => p.ProviderId == chunk.StorageProviderId);
                if (provider == null)
                {
                    _logger.LogError("Chunk için storage provider bulunamadı: {ChunkId}, ProviderId: {ProviderId}, CorrelationId: {CorrelationId}", 
                        chunkId, chunk.StorageProviderId, correlationId);
                    return false;
                }
                
                // Storage'dan sil
                var success = await provider.DeleteChunkAsync(chunkId, chunk.StoragePath, correlationId, cancellationToken);
                
                if (success)
                {
                    // Metadata'yı da sil
                    await _chunkRepository.DeleteAsync(chunkId, correlationId, cancellationToken);
                    
                    _logger.LogInformation("Chunk başarıyla silindi: {ChunkId}, CorrelationId: {CorrelationId}", 
                        chunkId, correlationId);
                }
                else
                {
                    _logger.LogWarning("Chunk storage'dan silinemedi: {ChunkId}, CorrelationId: {CorrelationId}", 
                        chunkId, correlationId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk silinirken hata oluştu: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Parçaları birleştirerek orijinal dosyayı yeniden oluşturur ve checksum doğrulaması yapar (Optimize edilmiş)
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="outputStream">Çıktı dosyasının yazılacağı akış</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="validateChecksum">Checksum doğrulaması yapılıp yapılmayacağı</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Birleştirme ve doğrulama işleminin başarılı olup olmadığını gösteren değer</returns>
        public async Task<bool> MergeChunksWithValidationAsync(string fileId, Stream outputStream, Guid correlationId, bool validateChecksum = true, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya parçalarını birleştirme ve doğrulama işlemi başlatıldı: {FileId}, ValidateChecksum: {ValidateChecksum}, CorrelationId: {CorrelationId}", 
                fileId, validateChecksum, correlationId);

            try
            {
                // Önce dosya metadata'sını getir
                FileMetadata fileMetadata = null;
                if (validateChecksum)
                {
                    fileMetadata = await _fileRepository.GetByIdAsync(fileId, correlationId, cancellationToken);
                    if (fileMetadata == null)
                    {
                        _logger.LogError("Dosya metadata'sı bulunamadı: {FileId}, CorrelationId: {CorrelationId}", 
                            fileId, correlationId);
                        return false;
                    }
                }

                // Chunk'ları birleştir
                var initialPosition = outputStream.Position;
                var mergeSuccess = await MergeChunksAsync(fileId, outputStream, correlationId, cancellationToken);
                
                if (!mergeSuccess)
                {
                    _logger.LogError("Chunk birleştirme işlemi başarısız: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                    return false;
                }

                // Checksum doğrulaması yap
                if (validateChecksum && fileMetadata != null)
                {
                    _logger.LogInformation("Dosya checksum doğrulaması başlatılıyor: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);

                    // Output stream'i başa al
                    var currentPosition = outputStream.Position;
                    outputStream.Position = initialPosition;
                    
                    // Birleştirilen dosyanın checksum'ını hesapla
                    var calculatedChecksum = await CalculateChecksumAsync(outputStream, cancellationToken);
                    
                    // Stream pozisyonunu geri al
                    outputStream.Position = currentPosition;
                    
                    // Checksum'ları karşılaştır
                    if (!string.Equals(calculatedChecksum, fileMetadata.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError(
                            "Dosya checksum doğrulaması başarısız: {FileId}, Beklenen: {ExpectedChecksum}, Hesaplanan: {CalculatedChecksum}, CorrelationId: {CorrelationId}", 
                            fileId, fileMetadata.Checksum, calculatedChecksum, correlationId);
                        return false;
                    }
                    
                    _logger.LogInformation(
                        "Dosya checksum doğrulaması başarılı: {FileId}, Checksum: {Checksum}, CorrelationId: {CorrelationId}", 
                        fileId, calculatedChecksum, correlationId);
                }

                _logger.LogInformation("Dosya parçaları başarıyla birleştirildi ve doğrulandı: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya parçaları birleştirilirken ve doğrulanırken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Bir chunk için ayarlara göre sıkıştırma seviyesini belirler
        /// </summary>
        private CompressionLevel GetCompressionLevel()
        {
            return _chunkSettings.CompressionLevel switch
            {
                <= 3 => CompressionLevel.Fastest,
                >= 8 => CompressionLevel.SmallestSize,
                _ => CompressionLevel.Optimal // Varsayılan (4-7 arası)
            };
        }

        /// <summary>
        /// Bir stream için checksum hesaplar
        /// </summary>
        private async Task<string> CalculateChecksumAsync(Stream stream, CancellationToken cancellationToken)
        {
            var originalPosition = stream.Position;
            stream.Position = 0;

            try
            {
                using var sha256 = SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            finally
            {
                // Stream'i orijinal konumuna geri döndür
                stream.Position = originalPosition;
            }
        }
    }
}
