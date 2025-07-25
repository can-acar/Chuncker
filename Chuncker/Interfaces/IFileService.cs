using Chuncker.Models;

namespace Chuncker.Interfaces
{
    /// <summary>
    /// Dosya işlemleri için üst düzey servis arayüzü.
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// Bir dosyayı sisteme yükler, parçalar ve dağıtır.
        /// </summary>
        /// <param name="fileStream">Dosya akışı</param>
        /// <param name="fileName">Dosya adı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Yüklenen dosyanın metadata bilgisi</returns>
        Task<FileMetadata> UploadFileAsync(Stream fileStream, string fileName, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir dosyayı sistemden indirir.
        /// </summary>
        /// <param name="fileId">İndirilecek dosya kimliği</param>
        /// <param name="outputStream">Çıktı dosyasının yazılacağı akış</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İndirme işleminin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> DownloadFileAsync(string fileId, Stream outputStream, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sistemdeki tüm dosyaların metadata bilgilerini listeler.
        /// </summary>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Dosya metadata bilgilerinin listesi</returns>
        Task<IEnumerable<FileMetadata>> ListFilesAsync(Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir dosyayı sistemden siler.
        /// </summary>
        /// <param name="fileId">Silinecek dosya kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Silme işleminin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> DeleteFileAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir dosyanın metadata bilgilerini getirir.
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Dosya metadata bilgisi</returns>
        Task<FileMetadata> GetFileMetadataAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir dosyanın bütünlüğünü kontrol eder.
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Dosya bütünlüğünün doğru olup olmadığını gösteren değer</returns>
        Task<bool> VerifyFileIntegrityAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default);
    }
}
