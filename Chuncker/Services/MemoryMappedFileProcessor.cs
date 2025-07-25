using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace Chuncker.Services
{
    /// <summary>
    /// Memory-mapped file kullanarak büyük dosyaları işleyen yardımcı sınıf
    /// </summary>
    public class MemoryMappedFileProcessor : IDisposable
    {
        private readonly ILogger<MemoryMappedFileProcessor> _logger;
        private MemoryMappedFile _mappedFile;
        private bool _disposed = false;
        private string _tempFilePath;

        /// <summary>
        /// Yeni bir MemoryMappedFileProcessor örneği oluşturur
        /// </summary>
        /// <param name="logger">Logger</param>
        public MemoryMappedFileProcessor(ILogger<MemoryMappedFileProcessor> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Stream'i memory-mapped dosyaya dönüştürür
        /// </summary>
        /// <param name="sourceStream">Kaynak stream</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <returns>MemoryMappedViewStream olarak dosya içeriği</returns>
        public async Task<Stream> CreateFromStreamAsync(Stream sourceStream, Guid correlationId)
        {
            _logger.LogInformation("Memory-mapped dosya oluşturuluyor, CorrelationId: {CorrelationId}", correlationId);

            if (sourceStream == null)
                throw new ArgumentNullException(nameof(sourceStream));

            CleanupExistingMappedFile();

            try
            {
                // Geçici dosya oluştur
                _tempFilePath = Path.GetTempFileName();
                _logger.LogDebug("Geçici dosya oluşturuldu: {TempFilePath}, CorrelationId: {CorrelationId}", 
                    _tempFilePath, correlationId);

                // Stream içeriğini geçici dosyaya kopyala
                using (var fileStream = new FileStream(_tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    sourceStream.Position = 0;
                    await sourceStream.CopyToAsync(fileStream);
                }

                var fileInfo = new FileInfo(_tempFilePath);
                long fileSize = fileInfo.Length;

                // Memory-mapped file oluştur
                _mappedFile = MemoryMappedFile.CreateFromFile(
                    _tempFilePath, 
                    FileMode.Open,
                    null, // mapName
                    fileSize,
                    MemoryMappedFileAccess.ReadWrite);

                _logger.LogInformation("Memory-mapped dosya başarıyla oluşturuldu, Boyut: {FileSize} bytes, CorrelationId: {CorrelationId}", 
                    fileSize, correlationId);

                // Tüm dosyaya erişen view stream döndür
                return _mappedFile.CreateViewStream();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Memory-mapped dosya oluşturulurken hata oluştu, CorrelationId: {CorrelationId}", correlationId);
                CleanupExistingMappedFile();
                throw;
            }
        }

        /// <summary>
        /// Memory-mapped dosyanın belirli bir parçasını okur
        /// </summary>
        /// <param name="offset">Başlangıç konumu</param>
        /// <param name="size">Okunacak veri boyutu</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <returns>Okunan veri</returns>
        public Stream ReadChunk(long offset, long size, Guid correlationId)
        {
            if (_mappedFile == null)
                throw new InvalidOperationException("Henüz bir memory-mapped dosya oluşturulmadı.");

            _logger.LogDebug("Memory-mapped dosyadan parça okunuyor: Offset: {Offset}, Boyut: {Size}, CorrelationId: {CorrelationId}", 
                offset, size, correlationId);

            try
            {
                // Belirtilen konumdan başlayıp belirtilen boyutta veri okuyan view stream oluştur
                return _mappedFile.CreateViewStream(offset, size, MemoryMappedFileAccess.Read);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Memory-mapped dosyadan parça okunurken hata oluştu, CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }

        /// <summary>
        /// Memory-mapped dosyanın belirli bir parçasına yazar
        /// </summary>
        /// <param name="data">Yazılacak veri</param>
        /// <param name="offset">Başlangıç konumu</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <returns>Yazma işleminin sonucu</returns>
        public async Task<bool> WriteChunkAsync(Stream data, long offset, Guid correlationId)
        {
            if (_mappedFile == null)
                throw new InvalidOperationException("Henüz bir memory-mapped dosya oluşturulmadı.");

            _logger.LogDebug("Memory-mapped dosyaya parça yazılıyor: Offset: {Offset}, CorrelationId: {CorrelationId}", 
                offset, correlationId);

            try
            {
                using (var viewStream = _mappedFile.CreateViewStream(offset, data.Length, MemoryMappedFileAccess.Write))
                {
                    data.Position = 0;
                    await data.CopyToAsync(viewStream);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Memory-mapped dosyaya parça yazılırken hata oluştu, CorrelationId: {CorrelationId}", correlationId);
                return false;
            }
        }

        /// <summary>
        /// Memory-mapped dosyadan normal bir stream oluşturur
        /// </summary>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <returns>Dosya içeriğini içeren stream</returns>
        public Stream ToStream(Guid correlationId)
        {
            if (_mappedFile == null)
                throw new InvalidOperationException("Henüz bir memory-mapped dosya oluşturulmadı.");

            _logger.LogDebug("Memory-mapped dosya stream'e dönüştürülüyor, CorrelationId: {CorrelationId}", correlationId);

            try
            {
                var fileInfo = new FileInfo(_tempFilePath);
                var memoryStream = new MemoryStream();

                using (var viewStream = _mappedFile.CreateViewStream(0, fileInfo.Length, MemoryMappedFileAccess.Read))
                {
                    viewStream.CopyTo(memoryStream);
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Memory-mapped dosya stream'e dönüştürülürken hata oluştu, CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }

        /// <summary>
        /// Mevcut memory-mapped dosyayı ve geçici dosyayı temizler
        /// </summary>
        private void CleanupExistingMappedFile()
        {
            if (_mappedFile != null)
            {
                _mappedFile.Dispose();
                _mappedFile = null;
            }

            if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
            {
                try
                {
                    File.Delete(_tempFilePath);
                }
                catch
                {
                    // Silme hatalarını görmezden gel
                }
                _tempFilePath = null;
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
        /// <param name="disposing">Yönetilen kaynakları temizlenip temizlenmeyeceği</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CleanupExistingMappedFile();
                }

                _disposed = true;
            }
        }
    }
}
