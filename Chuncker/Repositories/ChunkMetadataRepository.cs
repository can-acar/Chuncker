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
    /// ChunkMetadata için MongoDB repository implementasyonu
    /// </summary>
    public class ChunkMetadataRepository : MongoRepositoryBase<ChunkMetadata>, IChunkMetadataRepository
    {
        public ChunkMetadataRepository(IConfiguration configuration, ILogger<ChunkMetadataRepository> logger)
            : base(configuration, logger, "ChunkMetadata")
        {
            // ChunkMetadata koleksiyonu için indeksleri oluştur
            // Temporarily disabled due to authentication issues
            // CreateIndexes();
        }

        /// <summary>
        /// Koleksiyon için gerekli indeksleri oluşturur
        /// </summary>
        private void CreateIndexes()
        {
            try
            {
                var indexKeysFileId = Builders<ChunkMetadata>.IndexKeys.Ascending(c => c.FileId);
                var indexKeysProviderId = Builders<ChunkMetadata>.IndexKeys.Ascending(c => c.StorageProviderId);
                var indexKeysSequence = Builders<ChunkMetadata>.IndexKeys.Ascending(c => c.SequenceNumber);
                var indexKeysCreatedAt = Builders<ChunkMetadata>.IndexKeys.Ascending(c => c.CreatedAt);

                var indexModelsTask = new List<CreateIndexModel<ChunkMetadata>>
                {
                    new CreateIndexModel<ChunkMetadata>(indexKeysFileId),
                    new CreateIndexModel<ChunkMetadata>(indexKeysProviderId),
                    new CreateIndexModel<ChunkMetadata>(indexKeysSequence),
                    new CreateIndexModel<ChunkMetadata>(indexKeysCreatedAt)
                };

                _collection.Indexes.CreateManyAsync(indexModelsTask).GetAwaiter().GetResult();
                _logger.LogInformation("ChunkMetadata koleksiyonu için indeksler oluşturuldu");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ChunkMetadata koleksiyonu için indeksler oluşturulamadı: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Bir chunk metaverisi nesnesini kimliğe göre getirir
        /// </summary>
        public override async Task<ChunkMetadata> GetByIdAsync(string id, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("ID'ye göre chunk metaverisi getiriliyor: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                
                var filter = Builders<ChunkMetadata>.Filter.Eq(c => c.Id, id);
                var result = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
                
                if (result == null)
                {
                    _logger.LogWarning("Chunk metaverisi bulunamadı: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                    return null;
                }
                
                _logger.LogInformation("Chunk metaverisi başarıyla getirildi: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk metaverisi getirilirken hata oluştu: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir chunk metaverisi nesnesini günceller
        /// </summary>
        public override async Task<bool> UpdateAsync(ChunkMetadata entity, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Chunk metaverisi güncelleniyor: {Id}, CorrelationId: {CorrelationId}", entity.Id, correlationId);
                
                entity.UpdatedAt = DateTime.UtcNow;
                var filter = Builders<ChunkMetadata>.Filter.Eq(c => c.Id, entity.Id);
                var result = await _collection.ReplaceOneAsync(filter, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken);
                
                var success = result.IsAcknowledged && result.ModifiedCount > 0;
                if (success)
                {
                    _logger.LogInformation("Chunk metaverisi başarıyla güncellendi: {Id}, CorrelationId: {CorrelationId}", entity.Id, correlationId);
                }
                else
                {
                    _logger.LogWarning("Chunk metaverisi güncellenemedi: {Id}, CorrelationId: {CorrelationId}", entity.Id, correlationId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk metaverisi güncellenirken hata oluştu: {Id}, CorrelationId: {CorrelationId}", entity.Id, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Bir chunk metaverisini siler
        /// </summary>
        public override async Task<bool> DeleteAsync(string id, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Chunk metaverisi siliniyor: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                
                var filter = Builders<ChunkMetadata>.Filter.Eq(c => c.Id, id);
                var result = await _collection.DeleteOneAsync(filter, cancellationToken);
                
                var success = result.IsAcknowledged && result.DeletedCount > 0;
                if (success)
                {
                    _logger.LogInformation("Chunk metaverisi başarıyla silindi: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                }
                else
                {
                    _logger.LogWarning("Chunk metaverisi silinemedi: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk metaverisi silinirken hata oluştu: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Belirli bir dosyaya ait tüm chunk'ları getirir
        /// </summary>
        public async Task<IEnumerable<ChunkMetadata>> GetChunksByFileIdAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Dosya ID'sine göre chunk'lar getiriliyor: {FileId}, CorrelationId: {CorrelationId}", fileId, correlationId);
                
                var filter = Builders<ChunkMetadata>.Filter.Eq(c => c.FileId, fileId);
                var sort = Builders<ChunkMetadata>.Sort.Ascending(c => c.SequenceNumber);
                
                var result = await _collection.Find(filter).Sort(sort).ToListAsync(cancellationToken);
                
                _logger.LogInformation("Toplam {Count} chunk bulundu, FileId: {FileId}, CorrelationId: {CorrelationId}", 
                    result.Count, fileId, correlationId);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk'lar getirilirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", fileId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Belirli bir storage provider'a ait tüm chunk'ları getirir
        /// </summary>
        public async Task<IEnumerable<ChunkMetadata>> GetChunksByProviderIdAsync(string storageProviderId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Storage provider ID'sine göre chunk'lar getiriliyor: {ProviderId}, CorrelationId: {CorrelationId}", 
                    storageProviderId, correlationId);
                
                var filter = Builders<ChunkMetadata>.Filter.Eq(c => c.StorageProviderId, storageProviderId);
                var result = await _collection.Find(filter).ToListAsync(cancellationToken);
                
                _logger.LogInformation("Toplam {Count} chunk bulundu, ProviderId: {ProviderId}, CorrelationId: {CorrelationId}", 
                    result.Count, storageProviderId, correlationId);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk'lar getirilirken hata oluştu: {ProviderId}, CorrelationId: {CorrelationId}", 
                    storageProviderId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Belirli bir dosyaya ait tüm chunk'ları siler
        /// </summary>
        public async Task<bool> DeleteChunksByFileIdAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Dosyaya ait chunk'lar siliniyor: {FileId}, CorrelationId: {CorrelationId}", fileId, correlationId);
                
                var filter = Builders<ChunkMetadata>.Filter.Eq(c => c.FileId, fileId);
                var result = await _collection.DeleteManyAsync(filter, cancellationToken);
                
                var success = result.IsAcknowledged && result.DeletedCount > 0;
                if (success)
                {
                    _logger.LogInformation("Dosyaya ait chunk'lar başarıyla silindi. Silinen chunk sayısı: {Count}, FileId: {FileId}, CorrelationId: {CorrelationId}", 
                        result.DeletedCount, fileId, correlationId);
                }
                else
                {
                    _logger.LogWarning("Dosyaya ait chunk'lar silinemedi veya hiç chunk bulunamadı: {FileId}, CorrelationId: {CorrelationId}", 
                        fileId, correlationId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosyaya ait chunk'lar silinirken hata oluştu: {FileId}, CorrelationId: {CorrelationId}", fileId, correlationId);
                throw;
            }
        }
    }
}
