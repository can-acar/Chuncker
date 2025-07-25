using System.Security.Cryptography;
using Chuncker.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Chuncker.Providers
{
    /// <summary>
    /// Dosya sistemi üzerinde chunk depolama işlemlerini gerçekleştiren storage provider
    /// </summary>
    public class FileSystemStorageProvider : IStorageProvider
    {
        private readonly string _basePath;
        private readonly ILogger<FileSystemStorageProvider> _logger;
        private bool _disposed = false;

        /// <summary>
        /// Yeni bir FileSystemStorageProvider örneği oluşturur
        /// </summary>
        /// <param name="configuration">Uygulama yapılandırması</param>
        /// <param name="logger">Logger</param>
        public FileSystemStorageProvider(
            IConfiguration configuration,
            ILogger<FileSystemStorageProvider> logger)
        {
            _logger = logger;
            _basePath = configuration.GetSection("StorageProviderSettings:FileSystemPath").Value ?? "./Storage/Files";

            // Depolama dizininin varlığını kontrol et ve yoksa oluştur
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }

        /// <summary>
        /// Storage provider'ın benzersiz kimliği
        /// </summary>
        public string ProviderId => "filesystem";

        /// <summary>
        /// Storage provider'ın türü
        /// </summary>
        public string ProviderType => "FileSystem";

        /// <summary>
        /// Bir veri parçasını dosya sistemine yazar
        /// </summary>
        /// <param name="chunkId">Yazılacak chunk'ın benzersiz kimliği</param>
        /// <param name="data">Yazılacak veri akışı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Depolamada kullanılan benzersiz yol</returns>
        public async Task<string> WriteChunkAsync(string chunkId, Stream data, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk yazılıyor: {ChunkId}, CorrelationId: {CorrelationId}", chunkId, correlationId);

            // Chunk'ın kaydedileceği dizin yolu oluştur
            var directory = Path.Combine(_basePath, GetChunkDirectoryName(chunkId));
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Dosya yolu oluştur
            var filePath = Path.Combine(directory, $"{chunkId}.chunk");
            
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await data.CopyToAsync(fileStream, cancellationToken);
                }

                _logger.LogInformation("Chunk başarıyla yazıldı: {ChunkId}, Yol: {FilePath}, CorrelationId: {CorrelationId}", 
                    chunkId, filePath, correlationId);
                
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk yazılırken hata oluştu: {ChunkId}, CorrelationId: {CorrelationId}", chunkId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir veri parçasını dosya sisteminden okur
        /// </summary>
        /// <param name="chunkId">Okunacak chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Okunan veri akışı</returns>
        public async Task<Stream> ReadChunkAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk okunuyor: {ChunkId}, Yol: {StoragePath}, CorrelationId: {CorrelationId}", 
                chunkId, storagePath, correlationId);

            string filePath = storagePath;
            
            // Eğer tam yol verilmezse, chunk ID'ye göre yol oluştur
            if (!File.Exists(filePath))
            {
                var directory = Path.Combine(_basePath, GetChunkDirectoryName(chunkId));
                filePath = Path.Combine(directory, $"{chunkId}.chunk");
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError("Chunk bulunamadı: {ChunkId}, Yol: {FilePath}, CorrelationId: {CorrelationId}", 
                        chunkId, filePath, correlationId);
                    throw new FileNotFoundException($"Chunk bulunamadı: {chunkId}", filePath);
                }

                // Dosya içeriğini belleğe oku
                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    await fileStream.CopyToAsync(memoryStream, cancellationToken);
                }

                // Okuma pozisyonunu başa al
                memoryStream.Position = 0;

                _logger.LogInformation("Chunk başarıyla okundu: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                
                return memoryStream;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                _logger.LogError(ex, "Chunk okunurken hata oluştu: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir veri parçasını dosya sisteminden siler
        /// </summary>
        /// <param name="chunkId">Silinecek chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        public Task<bool> DeleteChunkAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk siliniyor: {ChunkId}, Yol: {StoragePath}, CorrelationId: {CorrelationId}", 
                chunkId, storagePath, correlationId);

            string filePath = storagePath;
            
            // Eğer tam yol verilmezse, chunk ID'ye göre yol oluştur
            if (!File.Exists(filePath))
            {
                var directory = Path.Combine(_basePath, GetChunkDirectoryName(chunkId));
                filePath = Path.Combine(directory, $"{chunkId}.chunk");
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Silinecek chunk bulunamadı: {ChunkId}, Yol: {FilePath}, CorrelationId: {CorrelationId}", 
                        chunkId, filePath, correlationId);
                    return Task.FromResult(false);
                }

                File.Delete(filePath);
                _logger.LogInformation("Chunk başarıyla silindi: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);

                // Dizin boşsa sil
                var directory = Path.GetDirectoryName(filePath);
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk silinirken hata oluştu: {ChunkId}, CorrelationId: {CorrelationId}", 
                    chunkId, correlationId);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Bir veri parçasının dosya sisteminde var olup olmadığını kontrol eder
        /// </summary>
        /// <param name="chunkId">Kontrol edilecek chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Chunk'ın var olup olmadığını gösteren değer</returns>
        public Task<bool> ChunkExistsAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunk kontrol ediliyor: {ChunkId}, Yol: {StoragePath}, CorrelationId: {CorrelationId}", 
                chunkId, storagePath, correlationId);

            string filePath = storagePath;
            
            // Eğer tam yol verilmezse, chunk ID'ye göre yol oluştur
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                var directory = Path.Combine(_basePath, GetChunkDirectoryName(chunkId));
                filePath = Path.Combine(directory, $"{chunkId}.chunk");
            }

            bool exists = File.Exists(filePath);
            _logger.LogInformation("Chunk varlığı: {ChunkId}, Mevcut: {Exists}, CorrelationId: {CorrelationId}", 
                chunkId, exists, correlationId);

            return Task.FromResult(exists);
        }

        /// <summary>
        /// Chunk ID'den dizin adı oluşturur (klasörleri hashing ile gruplar)
        /// </summary>
        /// <param name="chunkId">Chunk ID</param>
        /// <returns>Dizin adı</returns>
        private string GetChunkDirectoryName(string chunkId)
        {
            // Chunk ID'yi ilk 2 karakter dizin olacak şekilde grupla
            if (chunkId.Length >= 2)
            {
                return chunkId.Substring(0, 2);
            }

            // ID çok kısa ise hash oluştur
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(chunkId));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 2).ToLowerInvariant();
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
                }

                _disposed = true;
            }
        }
    }
}
