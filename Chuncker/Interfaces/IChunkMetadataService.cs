using Chuncker.Models;

namespace Chuncker.Interfaces
{
    /// <summary>
    /// Chunk metadata servisi arayüzü
    /// </summary>
    public interface IChunkMetadataService
    {
        /// <summary>
        /// Chunk metaverisini ID'ye göre getirir
        /// </summary>
        /// <param name="id">Chunk metaverisi ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan chunk metaverisi</returns>
        Task<ChunkMetadata> GetByIdAsync(string id, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tüm chunk metaverilerini getirir
        /// </summary>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Chunk metaverileri listesi</returns>
        Task<IEnumerable<ChunkMetadata>> GetAllAsync(Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Yeni bir chunk metaverisi ekler
        /// </summary>
        /// <param name="entity">Eklenecek chunk metaverisi</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eklenen chunk metaverisi</returns>
        Task<ChunkMetadata> AddAsync(ChunkMetadata entity, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Chunk metaverisini günceller
        /// </summary>
        /// <param name="entity">Güncellenecek chunk metaverisi</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem başarılı mı</returns>
        Task<bool> UpdateAsync(ChunkMetadata entity, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Chunk metaverisini siler
        /// </summary>
        /// <param name="id">Silinecek chunk metaverisi ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem başarılı mı</returns>
        Task<bool> DeleteAsync(string id, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir dosyaya ait tüm chunk'ları getirir
        /// </summary>
        /// <param name="fileId">Dosya ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen chunk'ların listesi</returns>
        Task<IEnumerable<ChunkMetadata>> GetChunksByFileIdAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir storage provider'a ait tüm chunk'ları getirir
        /// </summary>
        /// <param name="storageProviderId">Storage provider ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen chunk'ların listesi</returns>
        Task<IEnumerable<ChunkMetadata>> GetChunksByProviderIdAsync(string storageProviderId, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir dosyaya ait tüm chunk'ları siler
        /// </summary>
        /// <param name="fileId">Dosya ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem başarılı mı</returns>
        Task<bool> DeleteChunksByFileIdAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default);
    }
}
