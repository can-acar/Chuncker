namespace Chuncker.Interfaces
{
    /// <summary>
    /// Farklı depolama mekanizmalarını temsil eden arayüz.
    /// Bu arayüz sayesinde farklı depolama türleri (dosya sistemi, MongoDB, vb.) için soyutlama sağlanır.
    /// </summary>
    public interface IStorageProvider : IDisposable
    {
        /// <summary>
        /// Depolama sağlayıcısının benzersiz kimliğini döndürür.
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Depolama sağlayıcısının türünü döndürür.
        /// </summary>
        string ProviderType { get; }

        /// <summary>
        /// Bir veri parçasını depolama sağlayıcısına yazar.
        /// </summary>
        /// <param name="chunkId">Yazılacak chunk'ın benzersiz kimliği</param>
        /// <param name="data">Yazılacak veri akışı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Depolamada kullanılan benzersiz yol veya tanımlayıcı</returns>
        Task<string> WriteChunkAsync(string chunkId, Stream data, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir veri parçasını depolama sağlayıcısından okur.
        /// </summary>
        /// <param name="chunkId">Okunacak chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol veya tanımlayıcı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Okunan veri akışı</returns>
        Task<Stream> ReadChunkAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir veri parçasını depolama sağlayıcısından siler.
        /// </summary>
        /// <param name="chunkId">Silinecek chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol veya tanımlayıcı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> DeleteChunkAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir veri parçasının depolama sağlayıcısında var olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="chunkId">Kontrol edilecek chunk'ın benzersiz kimliği</param>
        /// <param name="storagePath">Depolamada kullanılan yol veya tanımlayıcı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Chunk'ın var olup olmadığını gösteren değer</returns>
        Task<bool> ChunkExistsAsync(string chunkId, string storagePath, Guid correlationId, CancellationToken cancellationToken = default);
    }
}
