namespace Chuncker.Interfaces
{
    /// <summary>
    /// Cache servisi arayüzü
    /// </summary>
    public interface ICacheService : IDisposable
    {
        /// <summary>
        /// Cache'den veri almayı dener
        /// </summary>
        /// <typeparam name="T">Veri türü</typeparam>
        /// <param name="key">Cache anahtarı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <returns>Cache'den alınan veri veya null</returns>
        Task<T> GetAsync<T>(string key, Guid correlationId) where T : class;

        /// <summary>
        /// Veriyi cache'e ekler
        /// </summary>
        /// <typeparam name="T">Veri türü</typeparam>
        /// <param name="key">Cache anahtarı</param>
        /// <param name="value">Eklenecek veri</param>
        /// <param name="expiry">Geçerlilik süresi (null ise varsayılan değer kullanılır)</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        Task SetAsync<T>(string key, T value, TimeSpan? expiry, Guid correlationId) where T : class;

        /// <summary>
        /// Cache'den bir öğeyi siler
        /// </summary>
        /// <param name="key">Cache anahtarı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <returns>Başarılı olup olmadığını gösteren değer</returns>
        Task<bool> RemoveAsync(string key, Guid correlationId);

        /// <summary>
        /// Cache anahtarının geçerlilik süresini günceller
        /// </summary>
        /// <param name="key">Cache anahtarı</param>
        /// <param name="expiry">Yeni geçerlilik süresi</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <returns>Başarılı olup olmadığını gösteren değer</returns>
        Task<bool> RefreshExpiryAsync(string key, TimeSpan expiry, Guid correlationId);
    }
}
