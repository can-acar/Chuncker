using Chuncker.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Interfaces;

namespace Chuncker.Repositories
{
    /// <summary>
    /// Dosya sistemi nesne meta verisi repository'si
    /// </summary>
    public class FileMetadataRepository : MongoRepositoryBase<FileMetadata>, IFileMetadataRepository
    {
        /// <summary>
        /// Dosya sistemi nesne meta verisi repository'si oluşturur
        /// </summary>
        /// <param name="configuration">Uygulama yapılandırması</param>
        /// <param name="logger">Logger</param>
        public FileMetadataRepository(IConfiguration configuration, ILogger<FileMetadataRepository> logger) 
            : base(configuration, logger, "FileMetadata")
        {
            // İndeksleri oluştur
            var indexOptions = new CreateIndexOptions();
            var indexKeys = Builders<FileMetadata>.IndexKeys
                .Ascending(x => x.ParentId)
                .Ascending(x => x.FullPath)
                .Ascending(x => x.Type);
                
            var indexModel = new CreateIndexModel<FileMetadata>(indexKeys, indexOptions);
            _collection.Indexes.CreateOne(indexModel);
        }

        /// <summary>
        /// Öğeyi kimliğine göre getirir
        /// </summary>
        /// <param name="id">Öğenin kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan öğe</returns>
        public override async Task<FileMetadata> GetByIdAsync(string id, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Dosya sistemi nesnesi getiriliyor, Id: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                
                var result = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
                
                if (result != null)
                {
                    _logger.LogInformation("Dosya sistemi nesnesi bulundu, Id: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                }
                else
                {
                    _logger.LogWarning("Dosya sistemi nesnesi bulunamadı, Id: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya sistemi nesnesi getirilirken hata oluştu, Id: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
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
        public override async Task<bool> UpdateAsync(FileMetadata entity, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Dosya sistemi nesnesi güncelleniyor, Id: {Id}, CorrelationId: {CorrelationId}", entity.Id, correlationId);
                
                var result = await _collection.ReplaceOneAsync(x => x.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken);
                
                var success = result.IsAcknowledged && result.ModifiedCount > 0;
                if (success)
                {
                    _logger.LogInformation("Dosya sistemi nesnesi başarıyla güncellendi, Id: {Id}, CorrelationId: {CorrelationId}", entity.Id, correlationId);
                }
                else
                {
                    _logger.LogWarning("Dosya sistemi nesnesi güncellenemedi, Id: {Id}, CorrelationId: {CorrelationId}", entity.Id, correlationId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya sistemi nesnesi güncellenirken hata oluştu, Id: {Id}, CorrelationId: {CorrelationId}", entity.Id, correlationId);
                throw;
            }
        }
        
        /// <summary>
        /// Bir öğeyi siler
        /// </summary>
        /// <param name="id">Silinecek öğenin kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlemin başarılı olup olmadığını gösteren değer</returns>
        public override async Task<bool> DeleteAsync(string id, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Dosya sistemi nesnesi siliniyor, Id: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                
                var result = await _collection.DeleteOneAsync(x => x.Id == id, cancellationToken);
                
                var success = result.IsAcknowledged && result.DeletedCount > 0;
                if (success)
                {
                    _logger.LogInformation("Dosya sistemi nesnesi başarıyla silindi, Id: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                }
                else
                {
                    _logger.LogWarning("Dosya sistemi nesnesi silinemedi, Id: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya sistemi nesnesi silinirken hata oluştu, Id: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                throw;
            }
        }
        
        /// <summary>
        /// Verilen tam yola sahip nesnenin meta verisini getirir
        /// </summary>
        /// <param name="fullPath">Tam yol</param>
        /// <returns>Nesne meta verisi</returns>
        public async Task<FileMetadata> GetByFullPathAsync(string fullPath)
        {
            return await _collection.Find(x => x.FullPath == fullPath).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Verilen ebeveyn ID'sine sahip tüm nesneleri getirir
        /// </summary>
        /// <param name="parentId">Ebeveyn ID'si</param>
        /// <returns>Nesne meta verisi listesi</returns>
        public async Task<List<FileMetadata>> GetChildrenAsync(string parentId)
        {
            return await _collection.Find(x => x.ParentId == parentId).ToListAsync();
        }
        
        /// <summary>
        /// Verilen ebeveyn yolundaki tüm nesneleri getirir
        /// </summary>
        /// <param name="parentPath">Ebeveyn yolu</param>
        /// <returns>Nesne meta verisi listesi</returns>
        public async Task<List<FileMetadata>> GetByParentPathAsync(string parentPath)
        {
            return await _collection.Find(x => x.FullPath.StartsWith(parentPath + "/")).ToListAsync();
        }
        
        /// <summary>
        /// Belirli bir tipteki tüm nesneleri getirir
        /// </summary>
        /// <param name="type">Nesne tipi</param>
        /// <returns>Nesne meta verisi listesi</returns>
        public async Task<List<FileMetadata>> GetByTypeAsync(FileSystemObjectType type)
        {
            return await _collection.Find(x => x.Type == type).ToListAsync();
        }
        
        /// <summary>
        /// İndekslenmemiş tüm nesneleri getirir
        /// </summary>
        /// <returns>İndekslenmemiş nesne meta verisi listesi</returns>
        public async Task<List<FileMetadata>> GetNonIndexedAsync()
        {
            return await _collection.Find(x => x.IsIndexed == false).ToListAsync();
        }
        
        /// <summary>
        /// Verilen etiketlere sahip tüm nesneleri getirir
        /// </summary>
        /// <param name="tags">Etiketler</param>
        /// <returns>Nesne meta verisi listesi</returns>
        public async Task<List<FileMetadata>> GetByTagsAsync(List<string> tags)
        {
            return await _collection.Find(x => x.Tags.All(tag => tags.Contains(tag))).ToListAsync();
        }
    }
}
