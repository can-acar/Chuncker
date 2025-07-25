using Chuncker.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace Chuncker.Providers
{
    /// <summary>
    /// MongoDB GridFS üzerinde chunk depolama işlemlerini gerçekleştiren storage provider
    /// </summary>
    public class MongoDbStorageProvider : IStorageProvider
    {
        private readonly IMongoDatabase _database;
        private readonly IGridFSBucket _gridFs;
        private readonly ILogger<MongoDbStorageProvider> _logger;
        private bool _disposed = false;

        /// <summary>
        /// Yeni bir MongoDbStorageProvider örneği oluşturur
        /// </summary>
        /// <param name="configuration">Uygulama yapılandırması</param>
        /// <param name="logger">Logger</param>
        public MongoDbStorageProvider(
            IConfiguration configuration,
            ILogger<MongoDbStorageProvider> logger)
        {
            _logger = logger;
            
            // MongoDB bağlantısını ve GridFS bucket'ını oluştur
            var connectionString = configuration.GetConnectionString("MongoDB");
            var databaseName = configuration.GetSection("DatabaseSettings:DatabaseName").Value ?? "ChunckerDB";
            
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
            
            // GridFS bucket'ını yapılandır
            var gridFsOptions = new GridFSBucketOptions
            {
                BucketName = "chunks",
                ChunkSizeBytes = 1048576 // 1 MB
            };
            
            _gridFs = new GridFSBucket(_database, gridFsOptions);
        }

        /// <summary>
        /// Storage provider'ın benzersiz kimliği
        /// </summary>
        public string ProviderId => "mongodb";

        /// <summary>
        /// Storage provider'ın türü
        /// </summary>
        public string ProviderType => "MongoDB";

        /// <summary>
        /// Bir veri parçasını MongoDB GridFS'e yazar
        /// </summary>
        /// <param name="chunkId">Yazılacak chunk'ın benzersiz kimliği</param>
        /// <param name="data">Yazılacak veri akışı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Depolamada kullanılan benzersiz yol (ObjectId)</returns>
        public async Task<string> WriteChunkAsync(string chunkId, Stream data, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk MongoDB'ye yazılıyor: {ChunkId}, CorrelationId: {CorrelationId}", chunkId, correlationId);

            try
            {
                // GridFS için metadata hazırla
                var metadata = new BsonDocument
                {
                    { "chunkId", chunkId },
                    { "correlationId", correlationId.ToString() },
                    { "createdAt", DateTime.UtcNow }
                };

                // GridFS'e yükle
                var options = new GridFSUploadOptions
                {
                    Metadata = metadata
                };

                var objectId = await _gridFs.UploadFromStreamAsync(chunkId, data, options, cancellationToken);
                var objectIdString = objectId.ToString();

                _logger.LogInformation(
                    "Chunk başarıyla MongoDB'ye yazıldı: {ChunkId}, ObjectId: {ObjectId}, CorrelationId: {CorrelationId}", 
                    chunkId, objectIdString, correlationId);

                return objectIdString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk MongoDB'ye yazılırken hata oluştu: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir veri parçasını MongoDB GridFS'ten okur
        /// </summary>
        /// <param name="chunkId">Okunacak chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol (ObjectId)</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Okunan veri akışı</returns>
        public async Task<Stream> ReadChunkAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk MongoDB'den okunuyor: {ChunkId}, ObjectId: {ObjectId}, CorrelationId: {CorrelationId}", 
                chunkId, storagePath, correlationId);

            try
            {
                // ObjectId ile mi yoksa ChunkId ile mi arayacağız?
                ObjectId objectId;
                
                if (ObjectId.TryParse(storagePath, out objectId))
                {
                    // ObjectId verilmişse doğrudan kullan
                    var stream = new MemoryStream();
                    await _gridFs.DownloadToStreamAsync(objectId, stream, null, cancellationToken);
                    stream.Position = 0;
                    
                    _logger.LogInformation(
                        "Chunk başarıyla MongoDB'den okundu (ObjectId): {ChunkId}, ObjectId: {ObjectId}, CorrelationId: {CorrelationId}", 
                        chunkId, storagePath, correlationId);
                    
                    return stream;
                }
                else
                {
                    // ChunkId ile ara
                    var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, chunkId);
                    var fileInfo = await _gridFs.FindAsync(filter, null, cancellationToken).Result.FirstOrDefaultAsync(cancellationToken);

                    if (fileInfo == null)
                    {
                        _logger.LogError("Chunk MongoDB'de bulunamadı: {ChunkId}, CorrelationId: {CorrelationId}", 
                            chunkId, correlationId);
                        throw new FileNotFoundException($"Chunk bulunamadı: {chunkId}");
                    }

                    var stream = new MemoryStream();
                    await _gridFs.DownloadToStreamAsync(fileInfo.Id, stream, null, cancellationToken);
                    stream.Position = 0;
                    
                    _logger.LogInformation(
                        "Chunk başarıyla MongoDB'den okundu (ChunkId): {ChunkId}, ObjectId: {ObjectId}, CorrelationId: {CorrelationId}", 
                        chunkId, fileInfo.Id, correlationId);
                    
                    return stream;
                }
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                _logger.LogError(ex, "Chunk MongoDB'den okunurken hata oluştu: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir veri parçasını MongoDB GridFS'ten siler
        /// </summary>
        /// <param name="chunkId">Silinecek chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol (ObjectId)</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        public async Task<bool> DeleteChunkAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk MongoDB'den siliniyor: {ChunkId}, ObjectId: {ObjectId}, CorrelationId: {CorrelationId}", 
                chunkId, storagePath, correlationId);

            try
            {
                // ObjectId ile mi yoksa ChunkId ile mi arayacağız?
                if (ObjectId.TryParse(storagePath, out var objectId))
                {
                    // ObjectId verilmişse doğrudan kullan
                    await _gridFs.DeleteAsync(objectId, cancellationToken);
                    
                    _logger.LogInformation(
                        "Chunk başarıyla MongoDB'den silindi (ObjectId): {ChunkId}, ObjectId: {ObjectId}, CorrelationId: {CorrelationId}", 
                        chunkId, storagePath, correlationId);
                    
                    return true;
                }
                else
                {
                    // ChunkId ile ara
                    var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, chunkId);
                    var fileInfo = await _gridFs.FindAsync(filter, null, cancellationToken).Result.FirstOrDefaultAsync(cancellationToken);

                    if (fileInfo == null)
                    {
                        _logger.LogWarning("Silinecek chunk MongoDB'de bulunamadı: {ChunkId}, CorrelationId: {CorrelationId}", 
                            chunkId, correlationId);
                        return false;
                    }

                    await _gridFs.DeleteAsync(fileInfo.Id, cancellationToken);
                    
                    _logger.LogInformation(
                        "Chunk başarıyla MongoDB'den silindi (ChunkId): {ChunkId}, ObjectId: {ObjectId}, CorrelationId: {CorrelationId}", 
                        chunkId, fileInfo.Id, correlationId);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk MongoDB'den silinirken hata oluştu: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Bir veri parçasının MongoDB GridFS'te var olup olmadığını kontrol eder
        /// </summary>
        /// <param name="chunkId">Kontrol edilecek chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol (ObjectId)</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Chunk'ın var olup olmadığını gösteren değer</returns>
        public async Task<bool> ChunkExistsAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk varlığı MongoDB'de kontrol ediliyor: {ChunkId}, ObjectId: {ObjectId}, CorrelationId: {CorrelationId}", 
                chunkId, storagePath, correlationId);

            try
            {
                // ObjectId ile mi yoksa ChunkId ile mi arayacağız?
                if (ObjectId.TryParse(storagePath, out var objectId))
                {
                    // ObjectId verilmişse doğrudan kullan
                    var filter = Builders<GridFSFileInfo>.Filter.Eq("_id", objectId);
                    var cursor = await _gridFs.FindAsync(filter, null, cancellationToken);
                    var files = await cursor.ToListAsync(cancellationToken);
                    var count = files.Count;
                    var exists = count > 0;
                    
                    _logger.LogInformation(
                        "Chunk varlığı MongoDB'de kontrol edildi (ObjectId): {ChunkId}, ObjectId: {ObjectId}, Mevcut: {Exists}, CorrelationId: {CorrelationId}", 
                        chunkId, storagePath, exists, correlationId);
                    
                    return exists;
                }
                else
                {
                    // ChunkId ile ara
                    var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, chunkId);
                    var cursor = await _gridFs.FindAsync(filter, null, cancellationToken);
                    var files = await cursor.ToListAsync(cancellationToken);
                    var count = files.Count;
                    var exists = count > 0;
                    
                    _logger.LogInformation(
                        "Chunk varlığı MongoDB'de kontrol edildi (ChunkId): {ChunkId}, Mevcut: {Exists}, CorrelationId: {CorrelationId}", 
                        chunkId, exists, correlationId);
                    
                    return exists;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk varlığı MongoDB'de kontrol edilirken hata oluştu: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Kaynakları temizler
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Kaynakları temizler
        /// </summary>
        /// <param name="disposing">Yönetilen kaynakların temizlenip temizlenmeyeceği</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Yönetilen kaynakları temizle
                    // (MongoDB istemcisinin Dispose edilmesi gerekmiyor)
                }

                _disposed = true;
            }
        }
    }
}
