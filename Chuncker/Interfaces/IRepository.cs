namespace Chuncker.Interfaces
{
    /// <summary>
    /// Genel repository arayüzü
    /// </summary>
    /// <typeparam name="T">Repository'nin işleyeceği veri türü</typeparam>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Bir öğeyi kimliğine göre getirir
        /// </summary>
        /// <param name="id">Öğenin kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan öğe</returns>
        Task<T> GetByIdAsync(string id, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tüm öğeleri getirir
        /// </summary>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Öğelerin listesi</returns>
        Task<IEnumerable<T>> GetAllAsync(Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Yeni bir öğe ekler
        /// </summary>
        /// <param name="entity">Eklenecek öğe</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eklenen öğe</returns>
        Task<T> AddAsync(T entity, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir öğeyi günceller
        /// </summary>
        /// <param name="entity">Güncellenecek öğe</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> UpdateAsync(T entity, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir öğeyi siler
        /// </summary>
        /// <param name="id">Silinecek öğenin kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        Task<bool> DeleteAsync(string id, Guid correlationId, CancellationToken cancellationToken = default);
    }
}
