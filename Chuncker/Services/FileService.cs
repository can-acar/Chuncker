using Chuncker.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Linq;
using System.Runtime.CompilerServices;
using Chuncker.Applications.Events;
using Chuncker.Infsructures.Events;
using Chuncker.Infsructures.Logging;
using Chuncker.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Chuncker.Services
{
    /// <summary>
    /// Dosya işlemleri için üst düzey servis uygulaması (Optimize edilmiş)
    /// </summary>
    public class FileService : IFileService
    {
        private readonly IChunkManager _chunkManager;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly IChunkMetadataService _chunkMetadataService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<FileService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly SemaphoreSlim _parallelOperationsSemaphore;
        private readonly ArrayPool<byte> _arrayPool;

        // Cache için sabitler
        private const string FILE_METADATA_CACHE_KEY_PREFIX = "file:metadata:";
        private const string FILE_INTEGRITY_CACHE_KEY_PREFIX = "file:integrity:";
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(15);

        /// <summary>
        /// OptimizedFileService constructor
        /// </summary>
        public FileService(
            IChunkManager chunkManager,
            IFileMetadataService fileMetadataService,
            IChunkMetadataService chunkMetadataService,
            IEventPublisher eventPublisher,
            ILogger<FileService> logger,
            ILoggerFactory loggerFactory,
            IMemoryCache memoryCache)
        {
            _chunkManager = chunkManager;
            _fileMetadataService = fileMetadataService;
            _chunkMetadataService = chunkMetadataService;
            _eventPublisher = eventPublisher;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _memoryCache = memoryCache;
            
            // Paralel işlemler için semaphore
            _parallelOperationsSemaphore = new SemaphoreSlim(4, 4); // En fazla 4 eş zamanlı işlem
            
            // Buffer havuzu
            _arrayPool = ArrayPool<byte>.Shared;
        }

        /// <summary>
        /// Bir dosyayı sisteme yükler, parçalar ve dağıtır (Optimize edilmiş)
        /// </summary>
        public async Task<FileMetadata> UploadFileAsync(Stream fileStream, string fileName, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya yükleme işlemi başlatıldı: {FileName}, CorrelationId: {CorrelationId}", 
                fileName, correlationId);
            
            // MongoDB'ye işlem log kaydı ekle
            LoggingContext.LogOperation("FileUpload", $"{fileName} dosyası yükleniyor", correlationId);
                
            await using var activity = LoggingActivity.Start(_logger, "UploadFile", correlationId, $"File: {fileName}");

            try
            {
                // Hızlı checksum hesaplama
                var fileChecksum = await CalculateChecksumFastAsync(fileStream, correlationId, cancellationToken);
                
                // Stream'i başlangıca geri sar
                fileStream.Position = 0;

                // Dosya metaverisi oluştur
                var fileMetadata = new FileMetadata
                {
                    Id = Guid.NewGuid().ToString(),
                    FileName = fileName,
                    FileSize = fileStream.Length,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Status = FileStatus.Processing,
                    Checksum = fileChecksum
                };

                // Dosya metaverisini veritabanına kaydet
                await _fileMetadataService.AddAsync(fileMetadata, correlationId, cancellationToken);

                // Memory-mapped file kullanarak dosyayı chunk'lara böl
                var fileProcessor = new MemoryMappedFileProcessor(_loggerFactory.CreateLogger<MemoryMappedFileProcessor>());
                
                try
                {
                    // Dosyayı memory-mapped formata dönüştür
                    await using var mappedStream = await fileProcessor.CreateFromStreamAsync(fileStream, correlationId);
                    
                    // Dosyayı chunk'lara böl ve sakla
                    var updatedFileMetadata = await _chunkManager.SplitFileAsync(mappedStream, fileMetadata.Id,fileMetadata.FileName, correlationId, cancellationToken);
                    
                    // Dosya durumunu güncelle
                    fileMetadata.Status = FileStatus.Completed;
                    fileMetadata.ChunkCount = updatedFileMetadata.ChunkCount;
                    await _fileMetadataService.UpdateAsync(fileMetadata, correlationId, cancellationToken);
                    
                    // Dosyayı önbelleğe al
                    CacheFileMetadata(fileMetadata);
                    
                    // Dosya işlendi olayını yayınla
                    await _eventPublisher.PublishAsync(new FileProcessedEvent
                    {
                        FileId = fileMetadata.Id,
                        FileName = fileMetadata.FileName,
                        FileSize = fileMetadata.FileSize,
                        ChunkCount = fileMetadata.ChunkCount,
                        CorrelationId = correlationId
                    }, cancellationToken);
                }
                finally
                {
                    // Memory-mapped dosyayı temizle
                    fileProcessor.Dispose();
                }

                _logger.LogInformation("Dosya başarıyla yüklendi: {FileId}, {FileName}, ChunkCount: {ChunkCount}, CorrelationId: {CorrelationId}", 
                    fileMetadata.Id, fileName, fileMetadata.ChunkCount, correlationId);
                
                // MongoDB'ye başarılı işlem kaydı ekle
                LoggingContext.LogOperation("FileUploadSuccess", 
                    $"{fileName} dosyası başarıyla yüklendi (ID: {fileMetadata.Id}, Boyut: {fileMetadata.FileSize} bytes)", 
                    correlationId);

                return fileMetadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yüklenirken hata oluştu: {FileName}, CorrelationId: {CorrelationId}", 
                    fileName, correlationId);
                
                // MongoDB'ye hata kaydı ekle
                LoggingContext.LogOperation("FileUploadError", 
                    $"{fileName} dosyasının yüklenmesi sırasında hata oluştu: {ex.Message}", 
                    correlationId);
                
                throw;
            }
        }

        /// <summary>
        /// Bir dosyayı sistemden indirir (Optimize edilmiş)
        /// </summary>
        public async Task<bool> DownloadFileAsync(string fileId, Stream outputStream, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya indirme işlemi başlatıldı: {FileId}, CorrelationId: {CorrelationId}", 
                fileId, correlationId);
                
            // MongoDB'ye işlem log kaydı ekle
            LoggingContext.LogOperation("FileDownload", $"{fileId} ID'li dosya indiriliyor", correlationId);

            await using var activity = LoggingActivity.Start(_logger, "DownloadFile", correlationId, $"FileId: {fileId}");

            try
            {
                // Dosya metaverisini önbellekte ara veya veritabanından getir
                var fileMetadata = await GetFileMetadataCachedAsync(fileId, correlationId, cancellationToken);
                
                if (fileMetadata == null)
                {
                    _logger.LogWarning("İndirilecek dosya bulunamadı: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                    return false;
                }

                if (fileMetadata.Status != FileStatus.Completed)
                {
                    _logger.LogWarning("Dosya kullanılamaz durumda: {FileId}, Status: {Status}, CorrelationId: {CorrelationId}", 
                        fileId, fileMetadata.Status, correlationId);
                    return false;
                }

                // Chunk'ları getir ve birleştir
                var result = await _chunkManager.MergeChunksAsync(fileId, outputStream, correlationId, cancellationToken);

                if (result)
                {
                    _logger.LogInformation("Dosya başarıyla indirildi: {FileId}, {FileName}, CorrelationId: {CorrelationId}", 
                        fileMetadata.Id, fileMetadata.FileName, correlationId);
                }
                else
                {
                    _logger.LogWarning("Dosya indirme işlemi başarısız oldu: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya indirilirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Sistemdeki tüm dosyaların metadata bilgilerini listeler (Optimize edilmiş)
        /// </summary>
        public async Task<IEnumerable<FileMetadata>> ListFilesAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Tüm dosyalar listeleniyor, CorrelationId: {CorrelationId}", correlationId);
            
            await using var activity = LoggingActivity.Start(_logger, "ListFiles", correlationId);
            
            try
            {
                // Paging kullanarak tüm dosyaları getir
                var files = await _fileMetadataService.GetAllAsync(correlationId, cancellationToken);
                
                // Dosyaları önbelleğe al
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        CacheFileMetadata(file);
                    }
                }
                
                _logger.LogInformation("Toplam {Count} dosya listelendi, CorrelationId: {CorrelationId}", 
                    files?.Count() ?? 0, correlationId);
                
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosyalar listelenirken hata oluştu, CorrelationId: {CorrelationId}", 
                    correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir dosyayı sistemden siler (Optimize edilmiş)
        /// </summary>
        public async Task<bool> DeleteFileAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya silme işlemi başlatıldı: {FileId}, CorrelationId: {CorrelationId}", 
                fileId, correlationId);

            await using var activity = LoggingActivity.Start(_logger, "DeleteFile", correlationId, $"FileId: {fileId}");

            try
            {
                // Dosyanın mevcut olup olmadığını kontrol et
                var fileMetadata = await GetFileMetadataCachedAsync(fileId, correlationId, cancellationToken);
                
                if (fileMetadata == null)
                {
                    _logger.LogWarning("Silinecek dosya bulunamadı: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                    return false;
                }

                // Tüm chunk'ları sil
                var chunksDeleted = await _chunkManager.DeleteChunksAsync(fileId, correlationId, cancellationToken);
                
                if (!chunksDeleted)
                {
                    _logger.LogWarning("Bazı parçalar silinemedi: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                }

                // Dosya metaverisini sil
                var result = await _fileMetadataService.DeleteAsync(fileId, correlationId, cancellationToken);

                // Önbellekten sil
                RemoveFileMetadataFromCache(fileId);
                RemoveFileIntegrityFromCache(fileId);

                if (result)
                {
                    _logger.LogInformation("Dosya başarıyla silindi: {FileId}, {FileName}, CorrelationId: {CorrelationId}", 
                        fileId, fileMetadata.FileName, correlationId);
                }
                else
                {
                    _logger.LogWarning("Dosya silme işlemi başarısız oldu: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya silinirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Bir dosyanın metadata bilgilerini getirir (Optimize edilmiş)
        /// </summary>
        public async Task<FileMetadata> GetFileMetadataAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya metaverisi getiriliyor: {FileId}, CorrelationId: {CorrelationId}", 
                fileId, correlationId);
            
            try
            {
                // Önbellekten veya veritabanından getir
                var fileMetadata = await GetFileMetadataCachedAsync(fileId, correlationId, cancellationToken);
                
                if (fileMetadata == null)
                {
                    _logger.LogWarning("Dosya bulunamadı: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                    return null;
                }

                _logger.LogInformation("Dosya metaverisi başarıyla getirildi: {FileId}, {FileName}, CorrelationId: {CorrelationId}", 
                    fileId, fileMetadata.FileName, correlationId);
                
                return fileMetadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya metaverisi getirilirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir dosyanın bütünlüğünü kontrol eder (Optimize edilmiş)
        /// </summary>
        public async Task<bool> VerifyFileIntegrityAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dosya bütünlüğü kontrol ediliyor: {FileId}, CorrelationId: {CorrelationId}", 
                fileId, correlationId);

            await using var activity = LoggingActivity.Start(_logger, "VerifyFileIntegrity", correlationId, $"FileId: {fileId}");

            try
            {
                // Önbellekten bütünlük sonucunu kontrol et
                if (_memoryCache.TryGetValue($"{FILE_INTEGRITY_CACHE_KEY_PREFIX}{fileId}", out bool cachedResult))
                {
                    _logger.LogInformation("Dosya bütünlük sonucu önbellekten alındı: {FileId}, Sonuç: {Result}, CorrelationId: {CorrelationId}", 
                        fileId, cachedResult ? "Doğrulandı" : "Doğrulanmadı", correlationId);
                    return cachedResult;
                }
                
                // Dosya metaverisini getir
                var fileMetadata = await GetFileMetadataCachedAsync(fileId, correlationId, cancellationToken);
                
                if (fileMetadata == null)
                {
                    _logger.LogWarning("Bütünlük kontrolü için dosya bulunamadı: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                    return false;
                }

                if (string.IsNullOrEmpty(fileMetadata.Checksum))
                {
                    _logger.LogWarning("Dosya için checksum değeri yok: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                    return false;
                }

                // Semaphore ile eş zamanlı dosya doğrulama işlemlerini sınırla
                await _parallelOperationsSemaphore.WaitAsync(cancellationToken);
                
                try
                {
                    // Geçici bir akış oluştur ve dosyayı birleştir
                    using var memoryStream = new MemoryStream();
                    var mergeResult = await _chunkManager.MergeChunksAsync(fileId, memoryStream, correlationId, cancellationToken);
                    
                    if (!mergeResult)
                    {
                        _logger.LogWarning("Dosya birleştirilemedi, bütünlük kontrolü başarısız: {FileId}, CorrelationId: {CorrelationId}", 
                            fileId, correlationId);
                        return false;
                    }

                    // Stream'i başlangıca geri sar
                    memoryStream.Position = 0;
                    
                    // Birleştirilmiş dosyanın checksum'ını hesapla
                    var calculatedChecksum = await CalculateChecksumFastAsync(memoryStream, correlationId, cancellationToken);
                    
                    // Checksumları karşılaştır
                    var isValid = string.Equals(fileMetadata.Checksum, calculatedChecksum, StringComparison.OrdinalIgnoreCase);
                    
                    _logger.LogInformation(
                        "Dosya bütünlük kontrolü {Result}: {FileId}, ExpectedChecksum: {ExpectedChecksum}, ActualChecksum: {ActualChecksum}, CorrelationId: {CorrelationId}",
                        isValid ? "başarılı" : "başarısız", fileId, fileMetadata.Checksum, calculatedChecksum, correlationId);
                    
                    // Sonucu önbelleğe al
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CACHE_DURATION,
                        Size = 1
                    };
                    _memoryCache.Set($"{FILE_INTEGRITY_CACHE_KEY_PREFIX}{fileId}", isValid, cacheOptions);
                    
                    return isValid;
                }
                finally
                {
                    // Semaphore'u serbest bırak
                    _parallelOperationsSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya bütünlüğü kontrol edilirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", 
                    fileId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Bir dosya için SHA256 checksum hızlı hesaplama (ArrayPool kullanarak)
        /// </summary>
        private async Task<string> CalculateChecksumFastAsync(Stream stream, Guid correlationId, CancellationToken cancellationToken)
        {
            try
            {
                var originalPosition = stream.Position;
                stream.Position = 0;
                
                // ArrayPool'dan buffer al
                byte[] buffer = null;
                try
                {
                    // 8 KB buffer kullan
                    const int BufferSize = 8192;
                    buffer = _arrayPool.Rent(BufferSize);
                    
                    using var sha256 = SHA256.Create();
                    
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
                    {
                        sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                    }
                    
                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    var hashBytes = sha256.Hash;
                    
                    var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    
                    return hash;
                }
                finally
                {
                    // Stream'i orijinal konumuna geri döndür
                    stream.Position = originalPosition;
                    
                    // Buffer'ı havuza geri döndür
                    if (buffer != null)
                        _arrayPool.Return(buffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checksum hesaplanırken hata oluştu, CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }
        
        /// <summary>
        /// Dosya metaverisini önbellekten veya veritabanından getir
        /// </summary>
        private async Task<FileMetadata> GetFileMetadataCachedAsync(string fileId, Guid correlationId, CancellationToken cancellationToken)
        {
            // Önbellekten kontrol et
            if (_memoryCache.TryGetValue($"{FILE_METADATA_CACHE_KEY_PREFIX}{fileId}", out FileMetadata cachedMetadata))
            {
                _logger.LogDebug("Dosya metaverisi önbellekten alındı: {FileId}, CorrelationId: {CorrelationId}", fileId, correlationId);
                return cachedMetadata;
            }
            
            // Veritabanından getir
            var fileMetadata = await _fileMetadataService.GetByIdAsync(fileId, correlationId, cancellationToken);
            
            // Önbelleğe al
            if (fileMetadata != null)
            {
                CacheFileMetadata(fileMetadata);
            }
            
            return fileMetadata;
        }
        
        /// <summary>
        /// Dosya metaverisini önbelleğe al
        /// </summary>
        private void CacheFileMetadata(FileMetadata fileMetadata)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CACHE_DURATION,
                Size = 1 // Cache size limit için gerekli
            };
            
            _memoryCache.Set($"{FILE_METADATA_CACHE_KEY_PREFIX}{fileMetadata.Id}", fileMetadata, cacheEntryOptions);
        }
        
        /// <summary>
        /// Dosya metaverisini önbellekten sil
        /// </summary>
        private void RemoveFileMetadataFromCache(string fileId)
        {
            _memoryCache.Remove($"{FILE_METADATA_CACHE_KEY_PREFIX}{fileId}");
        }
        
        /// <summary>
        /// Dosya bütünlük sonucunu önbellekten sil
        /// </summary>
        private void RemoveFileIntegrityFromCache(string fileId)
        {
            _memoryCache.Remove($"{FILE_INTEGRITY_CACHE_KEY_PREFIX}{fileId}");
        }
    }

    /// <summary>
    /// Performans izleme için yardımcı sınıf
    /// </summary>
    internal class LoggingActivity : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly string _activityName;
        private readonly Guid _correlationId;
        private readonly string _additionalInfo;
        private readonly System.Diagnostics.Stopwatch _stopwatch;

        private LoggingActivity(ILogger logger, string activityName, Guid correlationId, string additionalInfo = null)
        {
            _logger = logger;
            _activityName = activityName;
            _correlationId = correlationId;
            _additionalInfo = additionalInfo;
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        public static LoggingActivity Start(ILogger logger, string activityName, Guid correlationId, string additionalInfo = null)
        {
            var activity = new LoggingActivity(logger, activityName, correlationId, additionalInfo);
            
            logger.LogInformation(
                "Activity başlatıldı: {ActivityName}, CorrelationId: {CorrelationId}{AdditionalInfo}",
                activityName, 
                correlationId,
                additionalInfo != null ? $", {additionalInfo}" : string.Empty);
                
            return activity;
        }

        public async ValueTask DisposeAsync()
        {
            _stopwatch.Stop();
            
            _logger.LogInformation(
                "Activity tamamlandı: {ActivityName}, Süre: {ElapsedMilliseconds}ms, CorrelationId: {CorrelationId}{AdditionalInfo}",
                _activityName, 
                _stopwatch.ElapsedMilliseconds, 
                _correlationId,
                _additionalInfo != null ? $", {_additionalInfo}" : string.Empty);
                
            await Task.CompletedTask; // Async yapı için
        }
    }
}
