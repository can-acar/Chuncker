using Chuncker.Models;

namespace Chuncker.Interfaces
{
    /// <summary>
    /// Chunk metadata repository arayüzü
    /// </summary>
    public interface IChunkMetadataRepository : IRepository<ChunkMetadata>
    {
        /// <summary>
        /// Belirli bir dosyaya ait tüm chunk'ları getirir
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen chunk'ların listesi</returns>
        Task<IEnumerable<ChunkMetadata>> GetChunksByFileIdAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir storage provider'a ait tüm chunk'ları getirir
        /// </summary>
        /// <param name="storageProviderId">Storage provider kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen chunk'ların listesi</returns>
        Task<IEnumerable<ChunkMetadata>> GetChunksByProviderIdAsync(string storageProviderId, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir dosyaya ait tüm chunk'ları siler
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> DeleteChunksByFileIdAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default);
    }
}
