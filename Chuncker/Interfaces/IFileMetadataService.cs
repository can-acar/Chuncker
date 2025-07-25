using Chuncker.Models;

namespace Chuncker.Interfaces
{
    /// <summary>
    /// Dosya sistemi nesnelerinin meta verilerini yöneten servis arayüzü
    /// </summary>
    public interface IFileMetadataService
    {
        /// <summary>
        /// Verilen dizindeki tüm dosya ve klasörleri tarar ve meta verilerini kaydeder
        /// </summary>
        /// <param name="path">Taranacak dizin yolu</param>
        /// <param name="recursive">Alt dizinleri de tarayıp taramayacağı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan nesnelerin meta verileri</returns>
        Task<List<FileMetadata>> ScanDirectoryAsync(string path, bool recursive, Guid correlationId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Verilen yoldaki dosya veya klasörün meta verisini getirir, yoksa oluşturur
        /// </summary>
        /// <param name="path">Dosya veya klasör yolu</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Nesnenin meta verisi</returns>
        Task<FileMetadata> GetOrCreateObjectMetadataAsync(string path, Guid correlationId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Verilen ID'ye sahip nesnenin meta verisini getirir
        /// </summary>
        /// <param name="id">Nesne ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Nesnenin meta verisi</returns>
        Task<FileMetadata> GetByIdAsync(string id, Guid correlationId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Verilen yola sahip nesnenin meta verisini getirir
        /// </summary>
        /// <param name="path">Nesne yolu</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Nesnenin meta verisi</returns>
        Task<FileMetadata> GetByPathAsync(string path, Guid correlationId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Verilen klasör ID'sine sahip klasörün alt klasör ve dosyalarını getirir
        /// </summary>
        /// <param name="parentId">Ebeveyn klasör ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Alt nesnelerin meta verileri</returns>
        Task<List<FileMetadata>> GetChildrenAsync(string parentId, Guid correlationId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Nesnenin meta verisini günceller
        /// </summary>
        /// <param name="metadata">Güncellenecek meta veri</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem sonucu</returns>
        Task<bool> UpdateAsync(FileMetadata metadata, Guid correlationId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Nesnenin meta verisini siler
        /// </summary>
        /// <param name="id">Nesne ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem sonucu</returns>
        Task<bool> DeleteAsync(string id, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tüm dosya metaverilerini getirir
        /// </summary>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Dosya metaverileri listesi</returns>
        Task<IEnumerable<FileMetadata>> GetAllAsync(Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Yeni bir dosya metaverisi ekler
        /// </summary>
        /// <param name="entity">Eklenecek dosya metaverisi</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eklenen dosya metaverisi</returns>
        Task<FileMetadata> AddAsync(FileMetadata entity, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dosya adı desenine göre dosyaları bulur
        /// </summary>
        /// <param name="fileNamePattern">Dosya adı deseni</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen dosya metaverileri listesi</returns>
        Task<IEnumerable<FileMetadata>> FindByFileNameAsync(string fileNamePattern, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir tarih aralığında yüklenen dosyaları bulur
        /// </summary>
        /// <param name="startDate">Başlangıç tarihi</param>
        /// <param name="endDate">Bitiş tarihi</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen dosya metaverileri listesi</returns>
        Task<IEnumerable<FileMetadata>> FindByDateRangeAsync(DateTime startDate, DateTime endDate, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verilen dizindeki tüm dosya ve klasörleri tarar, meta verilerini kaydeder ve isteğe bağlı olarak dosya içeriklerini işler
        /// </summary>
        /// <param name="path">Taranacak dizin yolu</param>
        /// <param name="recursive">Alt dizinleri de tarayıp taramayacağı</param>
        /// <param name="processContent">Dosya içeriklerini işleyip chunk'lara böl</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="progressReporter">Progress reporter (opsiyonel)</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan nesnelerin meta verileri</returns>
        Task<List<FileMetadata>> ScanAndProcessDirectoryAsync(string path, bool recursive, bool processContent, Guid correlationId, IProgressReporter progressReporter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dosya içeriğini analiz eder ve meta verilerini günceller
        /// </summary>
        /// <param name="filePath">Analiz edilecek dosya yolu</param>
        /// <param name="processContent">İçeriği chunk'lara böl</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Güncellenmiş dosya meta verisi</returns>
        Task<FileMetadata> AnalyzeFileContentAsync(string filePath, bool processContent, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verilen dizindeki tüm dosya ve klasörleri paralel olarak tarar, meta verilerini kaydeder ve isteğe bağlı olarak dosya içeriklerini işler
        /// </summary>
        /// <param name="path">Taranacak dizin yolu</param>
        /// <param name="recursive">Alt dizinleri de tarayıp taramayacağı</param>
        /// <param name="processContent">Dosya içeriklerini işleyip chunk'lara böl</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="progressReporter">Progress reporter (opsiyonel)</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan nesnelerin meta verileri</returns>
        Task<List<FileMetadata>> ScanDirectoryParallelAsync(string path, bool recursive, bool processContent, Guid correlationId, IProgressReporter progressReporter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checksum değerine göre yinelenen dosyaları bulur
        /// </summary>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Yinelenen dosyaların listesi</returns>
        Task<Dictionary<string, List<FileMetadata>>> FindDuplicatesByChecksumAsync(Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dosya adı veya pattern ile arama yapar
        /// </summary>
        /// <param name="pattern">Arama pattern'i, wildcard içerebilir (örn. *.txt)</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen dosyalar</returns>
        Task<IEnumerable<FileMetadata>> FindFilesAsync(string pattern, Guid correlationId, CancellationToken cancellationToken = default);
    }
}
