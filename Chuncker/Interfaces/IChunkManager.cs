using Chuncker.Models;

namespace Chuncker.Interfaces
{
    /// <summary>
    /// Dosya parçalama (chunking) işlemlerini yöneten arayüz.
    /// </summary>
    public interface IChunkManager
    {
        /// <summary>
        /// Bir dosyayı parçalara böler ve her parçayı depolama sağlayıcılarına dağıtır.
        /// </summary>
        /// <param name="fileStream">Parçalanacak dosya akışı</param>
        /// <param name="fileId"></param>
        /// <param name="fileName">Dosya adı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Oluşturulan dosya metadata bilgisi</returns>
        Task<FileMetadata> SplitFileAsync(Stream fileStream, string fileId, string fileName, Guid correlationId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir dosyayı parçalara böler ve her parçayı depolama sağlayıcılarına dağıtır (belirtilen dosya ID'si ile).
        /// </summary>
        /// <param name="fileStream">Parçalanacak dosya akışı</param>
        /// <param name="fileId">Kullanılacak dosya kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="useExistingFileId">True ise fileId parameterini var olan dosya ID'si olarak kullan</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Güncellenen dosya metadata bilgisi</returns>
        Task<Models.FileMetadata> SplitFileWithExistingIdAsync(Stream fileStream, string fileId, Guid correlationId, bool useExistingFileId = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parçaları birleştirerek orijinal dosyayı yeniden oluşturur.
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="outputStream">Çıktı dosyasının yazılacağı akış</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Birleştirme işleminin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> MergeChunksAsync(string fileId, Stream outputStream, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir dosyaya ait tüm parçaları siler.
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Silme işleminin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> DeleteChunksAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir chunk'ı siler.
        /// </summary>
        /// <param name="chunkId">Chunk kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Silme işleminin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> DeleteChunkAsync(string chunkId, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Optimal chunk boyutunu hesaplar.
        /// </summary>
        /// <param name="fileSize">Dosya boyutu</param>
        /// <returns>Önerilen chunk boyutu</returns>
        long CalculateOptimalChunkSize(long fileSize);

        /// <summary>
        /// Parçaları birleştirerek orijinal dosyayı yeniden oluşturur ve checksum doğrulaması yapar.
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="outputStream">Çıktı dosyasının yazılacağı akış</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="validateChecksum">Checksum doğrulaması yapılıp yapılmayacağı</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Birleştirme ve doğrulama işleminin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> MergeChunksWithValidationAsync(string fileId, Stream outputStream, Guid correlationId, bool validateChecksum = true, CancellationToken cancellationToken = default);
    }
}
