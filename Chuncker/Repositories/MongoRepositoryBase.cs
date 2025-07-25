using Chuncker.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Interfaces;

namespace Chuncker.Repositories
{
    /// <summary>
    /// MongoDB için temel repository sınıfı
    /// </summary>
    /// <typeparam name="T">Repository'nin işleyeceği veri türü</typeparam>
    public abstract class MongoRepositoryBase<T> : IRepository<T> where T : class
    {
        protected readonly IMongoCollection<T> _collection;
        protected readonly ILogger _logger;
        protected readonly string _collectionName;

        protected MongoRepositoryBase(
            IConfiguration configuration,
            ILogger logger,
            string collectionName)
        {
            _logger = logger;
            _collectionName = collectionName;

            var connectionString = configuration.GetConnectionString("MongoDB");
            var databaseName = configuration.GetSection("DatabaseSettings:DatabaseName").Value ?? "ChunckerDB";

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("MongoDB bağlantı dizesi yapılandırmada bulunamadı.");
            }

            var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
            clientSettings.ClusterConfigurator = cb =>
            {
                cb.Subscribe<MongoDB.Driver.Core.Events.CommandStartedEvent>(e =>
                {
                    _logger.LogDebug("MongoDB Command Started: {CommandName}, RequestId: {RequestId}", e.CommandName, e.RequestId);
                });
                cb.Subscribe<MongoDB.Driver.Core.Events.CommandSucceededEvent>(e =>
                {
                    _logger.LogDebug("MongoDB Command Succeeded: {CommandName}, RequestId: {RequestId}, Duration: {Duration}", e.CommandName, e.RequestId, e.Duration);
                });
                cb.Subscribe<MongoDB.Driver.Core.Events.CommandFailedEvent>(e =>
                {
                    _logger.LogError(e.Failure, "MongoDB Command Failed: {CommandName}, RequestId: {RequestId}, Duration: {Duration}", e.CommandName, e.RequestId, e.Duration);
                });
            };

            var client = new MongoClient(clientSettings);
            var database = client.GetDatabase(databaseName);
            _collection = database.GetCollection<T>(collectionName);
            
            _logger.LogInformation("MongoDB repository oluşturuldu: {CollectionName}", collectionName);
        }

        /// <summary>
        /// Bir öğeyi kimliğine göre getirir
        /// </summary>
        /// <param name="id">Öğenin kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan öğe</returns>
        public abstract Task<T> GetByIdAsync(string id, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tüm öğeleri getirir
        /// </summary>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Öğelerin listesi</returns>
        public virtual async Task<IEnumerable<T>> GetAllAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Tüm öğeler getiriliyor, Koleksiyon: {CollectionName}, CorrelationId: {CorrelationId}", 
                    _collectionName, correlationId);
                
                var result = await _collection.Find(new BsonDocument()).ToListAsync(cancellationToken);
                
                _logger.LogInformation("Toplam {Count} öğe bulundu, CorrelationId: {CorrelationId}", 
                    result.Count, correlationId);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğeler getirilirken hata oluştu, Koleksiyon: {CollectionName}, CorrelationId: {CorrelationId}", 
                    _collectionName, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Yeni bir öğe ekler
        /// </summary>
        /// <param name="entity">Eklenecek öğe</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eklenen öğe</returns>
        public virtual async Task<T> AddAsync(T entity, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Yeni öğe ekleniyor, Koleksiyon: {CollectionName}, CorrelationId: {CorrelationId}", 
                    _collectionName, correlationId);
                
                await _collection.InsertOneAsync(entity, null, cancellationToken);
                
                _logger.LogInformation("Öğe başarıyla eklendi, CorrelationId: {CorrelationId}", correlationId);
                
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğe eklenirken hata oluştu, Koleksiyon: {CollectionName}, CorrelationId: {CorrelationId}", 
                    _collectionName, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir öğeyi günceller
        /// </summary>
        /// <param name="entity">Güncellenecek öğe</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        public abstract Task<bool> UpdateAsync(T entity, Guid correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir öğeyi siler
        /// </summary>
        /// <param name="id">Silinecek öğenin kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        public abstract Task<bool> DeleteAsync(string id, Guid correlationId, CancellationToken cancellationToken = default);
    }
}
