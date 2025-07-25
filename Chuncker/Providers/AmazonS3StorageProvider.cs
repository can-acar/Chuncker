using Amazon.S3;
using Amazon.S3.Model;
using Chuncker.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Chuncker.Providers
{
    /// <summary>
    /// Amazon S3 üzerinde chunk depolama işlemlerini gerçekleştiren storage provider
    /// </summary>
    public class AmazonS3StorageProvider : IStorageProvider, IDisposable
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _keyPrefix;
        private readonly ILogger<AmazonS3StorageProvider> _logger;
        private bool _disposed = false;

        /// <summary>
        /// Yeni bir AmazonS3StorageProvider örneği oluşturur
        /// </summary>
        /// <param name="s3Client">Amazon S3 client</param>
        /// <param name="configuration">Uygulama yapılandırması</param>
        /// <param name="logger">Logger</param>
        public AmazonS3StorageProvider(
            IAmazonS3 s3Client,
            IConfiguration configuration,
            ILogger<AmazonS3StorageProvider> logger)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // S3 yapılandırma ayarlarını oku
            var s3Settings = configuration.GetSection("StorageProviderSettings:AmazonS3");
            _bucketName = s3Settings["BucketName"] ?? throw new ArgumentException("S3 BucketName yapılandırması gereklidir");
            _keyPrefix = s3Settings["KeyPrefix"] ?? "chunks/";

            // Key prefix'in '/' ile bitmesini sağla
            if (!_keyPrefix.EndsWith("/"))
                _keyPrefix += "/";

            _logger.LogInformation("Amazon S3 Storage Provider başlatıldı: Bucket: {BucketName}, KeyPrefix: {KeyPrefix}", 
                _bucketName, _keyPrefix);
        }

        /// <summary>
        /// Storage provider'ın benzersiz kimliği
        /// </summary>
        public string ProviderId => "amazons3";

        /// <summary>
        /// Storage provider'ın türü
        /// </summary>
        public string ProviderType => "AmazonS3";

        /// <summary>
        /// Bir veri parçasını Amazon S3'e yazar
        /// </summary>
        /// <param name="chunkId">Yazılacak chunk'ın benzersiz kimliği</param>
        /// <param name="data">Yazılacak veri akışı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>S3'te depolamada kullanılan anahtar</returns>
        public async Task<string> WriteChunkAsync(string chunkId, Stream data, Guid correlationId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            _logger.LogInformation("S3'e chunk yazılıyor: ChunkId: {ChunkId}, CorrelationId: {CorrelationId}", 
                chunkId, correlationId);

            try
            {
                // S3 key'ini oluştur (prefix + chunkId + .chunk extension)
                var s3Key = $"{_keyPrefix}{GetChunkKeyName(chunkId)}.chunk";

                // PutObject request'i oluştur
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key,
                    InputStream = data,
                    ContentType = "application/octet-stream",
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                    Metadata =
                    {
                        ["ChunkId"] = chunkId,
                        ["CorrelationId"] = correlationId.ToString(),
                        ["UploadTimestamp"] = DateTime.UtcNow.ToString("O")
                    }
                };

                // S3'e yükle
                var response = await _s3Client.PutObjectAsync(putRequest, cancellationToken);

                _logger.LogInformation(
                    "Chunk başarıyla S3'e yazıldı: ChunkId: {ChunkId}, S3Key: {S3Key}, ETag: {ETag}, CorrelationId: {CorrelationId}",
                    chunkId, s3Key, response.ETag, correlationId);

                return s3Key;
            }
            catch (AmazonS3Exception s3Ex)
            {
                _logger.LogError(s3Ex,
                    "S3'e chunk yazılırken S3 hatası oluştu: ChunkId: {ChunkId}, ErrorCode: {ErrorCode}, CorrelationId: {CorrelationId}",
                    chunkId, s3Ex.ErrorCode, correlationId);
                throw new InvalidOperationException($"S3 chunk yazma hatası: {s3Ex.Message}", s3Ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "S3'e chunk yazılırken genel hata oluştu: ChunkId: {ChunkId}, CorrelationId: {CorrelationId}",
                    chunkId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Amazon S3'ten bir veri parçasını okur
        /// </summary>
        /// <param name="chunkId">Okunacak chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">S3'te depolamada kullanılan key (null ise chunkId'den üretilir)</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Okunan veri akışı</returns>
        public async Task<Stream> ReadChunkAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            _logger.LogInformation("S3'ten chunk okunuyor: ChunkId: {ChunkId}, StoragePath: {StoragePath}, CorrelationId: {CorrelationId}", 
                chunkId, storagePath, correlationId);

            try
            {
                // S3 key'ini belirle - storagePath varsa onu kullan, yoksa chunkId'den üret
                var s3Key = string.IsNullOrEmpty(storagePath) 
                    ? $"{_keyPrefix}{GetChunkKeyName(chunkId)}.chunk"
                    : storagePath;

                // GetObject request'i oluştur
                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };

                // S3'ten oku
                var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken);

                _logger.LogInformation(
                    "Chunk başarıyla S3'ten okundu: ChunkId: {ChunkId}, S3Key: {S3Key}, ContentLength: {ContentLength}, CorrelationId: {CorrelationId}",
                    chunkId, s3Key, response.ContentLength, correlationId);

                // Response stream'ini döndür (caller tarafından dispose edilecek)
                return response.ResponseStream;
            }
            catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "S3'te chunk bulunamadı: ChunkId: {ChunkId}, CorrelationId: {CorrelationId}",
                    chunkId, correlationId);
                return null;
            }
            catch (AmazonS3Exception s3Ex)
            {
                _logger.LogError(s3Ex,
                    "S3'ten chunk okunurken S3 hatası oluştu: ChunkId: {ChunkId}, ErrorCode: {ErrorCode}, CorrelationId: {CorrelationId}",
                    chunkId, s3Ex.ErrorCode, correlationId);
                throw new InvalidOperationException($"S3 chunk okuma hatası: {s3Ex.Message}", s3Ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "S3'ten chunk okunurken genel hata oluştu: ChunkId: {ChunkId}, CorrelationId: {CorrelationId}",
                    chunkId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Amazon S3'ten bir veri parçasını siler
        /// </summary>
        /// <param name="chunkId">Silinecek chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">S3'te depolamada kullanılan key (null ise chunkId'den üretilir)</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Silme işleminin başarılı olup olmadığı</returns>
        public async Task<bool> DeleteChunkAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            _logger.LogInformation("S3'ten chunk siliniyor: ChunkId: {ChunkId}, StoragePath: {StoragePath}, CorrelationId: {CorrelationId}", 
                chunkId, storagePath, correlationId);

            try
            {
                // S3 key'ini belirle - storagePath varsa onu kullan, yoksa chunkId'den üret
                var s3Key = string.IsNullOrEmpty(storagePath) 
                    ? $"{_keyPrefix}{GetChunkKeyName(chunkId)}.chunk"
                    : storagePath;

                // DeleteObject request'i oluştur
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };

                // S3'ten sil
                var response = await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);

                _logger.LogInformation(
                    "Chunk başarıyla S3'ten silindi: ChunkId: {ChunkId}, S3Key: {S3Key}, CorrelationId: {CorrelationId}",
                    chunkId, s3Key, correlationId);

                return true;
            }
            catch (AmazonS3Exception s3Ex)
            {
                _logger.LogError(s3Ex,
                    "S3'ten chunk silinirken S3 hatası oluştu: ChunkId: {ChunkId}, ErrorCode: {ErrorCode}, CorrelationId: {CorrelationId}",
                    chunkId, s3Ex.ErrorCode, correlationId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "S3'ten chunk silinirken genel hata oluştu: ChunkId: {ChunkId}, CorrelationId: {CorrelationId}",
                    chunkId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Chunk'ın var olup olmadığını kontrol eder
        /// </summary>
        /// <param name="chunkId">Kontrol edilecek chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">S3'te depolamada kullanılan key (null ise chunkId'den üretilir)</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Chunk'ın var olup olmadığı</returns>
        public async Task<bool> ChunkExistsAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            try
            {
                // S3 key'ini belirle - storagePath varsa onu kullan, yoksa chunkId'den üret
                var s3Key = string.IsNullOrEmpty(storagePath) 
                    ? $"{_keyPrefix}{GetChunkKeyName(chunkId)}.chunk"
                    : storagePath;

                // Head object ile existence kontrolü
                var headRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };

                await _s3Client.GetObjectMetadataAsync(headRequest, cancellationToken);

                _logger.LogDebug("Chunk S3'te mevcut: ChunkId: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                return true;
            }
            catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Chunk S3'te mevcut değil: ChunkId: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chunk existence kontrolü sırasında hata oluştu: ChunkId: {ChunkId}, CorrelationId: {CorrelationId}",
                    chunkId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Chunk ID'sinden S3 key name'i oluşturur
        /// </summary>
        /// <param name="chunkId">Chunk ID</param>
        /// <returns>S3 key name</returns>
        private string GetChunkKeyName(string chunkId)
        {
            if (string.IsNullOrEmpty(chunkId))
                throw new ArgumentException("Chunk ID boş olamaz", nameof(chunkId));

            // ChunkId'yi safe file name haline getir
            var safeChunkId = chunkId.Replace('/', '_').Replace('\\', '_');
            
            // Hiyerarşik yapı için ilk 2 karakteri kullan (performans için)
            if (safeChunkId.Length >= 2)
            {
                return $"{safeChunkId.Substring(0, 2)}/{safeChunkId}";
            }

            return safeChunkId;
        }

        /// <summary>
        /// Object disposed kontrolü
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AmazonS3StorageProvider));
        }

        /// <summary>
        /// Storage provider kaynaklarını temizler
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.LogInformation("Amazon S3 Storage Provider kapatılıyor");
                
                try
                {
                    _s3Client?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "S3 client dispose edilirken hata oluştu");
                }

                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~AmazonS3StorageProvider()
        {
            Dispose();
        }
    }
}