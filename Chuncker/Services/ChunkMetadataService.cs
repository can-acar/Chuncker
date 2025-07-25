using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Interfaces;
using Chuncker.Models;
using Microsoft.Extensions.Logging;

namespace Chuncker.Services
{
    /// <summary>
    /// Chunk metadata servisi
    /// </summary>
    public class ChunkMetadataService : IChunkMetadataService
    {
        private readonly IChunkMetadataRepository _repository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ChunkMetadataService> _logger;

        private const string CHUNK_METADATA_CACHE_KEY = "ChunkMetadata_";
        private const string FILE_CHUNKS_CACHE_KEY = "FileChunks_";
        private static readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(30);

        public ChunkMetadataService(
            IChunkMetadataRepository repository,
            ICacheService cacheService,
            ILogger<ChunkMetadataService> logger)
        {
            _repository = repository;
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// Chunk metaverisini ID'ye göre getirir (önce cache'e bakar)
        /// </summary>
        public async Task<ChunkMetadata> GetByIdAsync(string id, Guid correlationId, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetChunkCacheKey(id);
            
            // Önce cache'e bak
            var chunkMetadata = await _cacheService.GetAsync<ChunkMetadata>(cacheKey, correlationId);
            if (chunkMetadata != null)
            {
                _logger.LogInformation("Chunk metaverisi cache'den alındı: {Id}, CorrelationId: {CorrelationId}", id, correlationId);
                return chunkMetadata;
            }

            // Cache'de yoksa repository'den al
            chunkMetadata = await _repository.GetByIdAsync(id, correlationId, cancellationToken);
            if (chunkMetadata != null)
            {
                // Cache'e ekle
                await _cacheService.SetAsync(cacheKey, chunkMetadata, _defaultExpiry, correlationId);
            }

            return chunkMetadata;
        }

        /// <summary>
        /// Tüm chunk metaverilerini getirir
        /// </summary>
        public Task<IEnumerable<ChunkMetadata>> GetAllAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            return _repository.GetAllAsync(correlationId, cancellationToken);
        }

        /// <summary>
        /// Yeni bir chunk metaverisi ekler
        /// </summary>
        public async Task<ChunkMetadata> AddAsync(ChunkMetadata entity, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // ID atanmadıysa ata
            if (string.IsNullOrEmpty(entity.Id))
            {
                entity.Id = Guid.NewGuid().ToString();
            }
            
            // Oluşturma ve güncellenme zamanlarını ayarla
            if (entity.CreatedAt == default)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }
            
            entity.UpdatedAt = entity.CreatedAt;

            var result = await _repository.AddAsync(entity, correlationId, cancellationToken);
            
            // Chunk cache'ini güncelle
            var chunkCacheKey = GetChunkCacheKey(result.Id);
            await _cacheService.SetAsync(chunkCacheKey, result, _defaultExpiry, correlationId);
            
            // Dosya chunk'larının cache'ini sıfırla
            var fileCacheKey = GetFileChunksCacheKey(result.FileId);
            await _cacheService.RemoveAsync(fileCacheKey, correlationId);
            
            return result;
        }

        /// <summary>
        /// Chunk metaverisini günceller
        /// </summary>
        public async Task<bool> UpdateAsync(ChunkMetadata entity, Guid correlationId, CancellationToken cancellationToken = default)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            
            var result = await _repository.UpdateAsync(entity, correlationId, cancellationToken);
            if (result)
            {
                // Chunk cache'ini güncelle
                var chunkCacheKey = GetChunkCacheKey(entity.Id);
                await _cacheService.SetAsync(chunkCacheKey, entity, _defaultExpiry, correlationId);
                
                // Dosya chunk'larının cache'ini sıfırla
                var fileCacheKey = GetFileChunksCacheKey(entity.FileId);
                await _cacheService.RemoveAsync(fileCacheKey, correlationId);
            }
            
            return result;
        }

        /// <summary>
        /// Chunk metaverisini siler
        /// </summary>
        public async Task<bool> DeleteAsync(string id, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // Önce chunk'ın mevcut bilgilerini al
            var chunk = await _repository.GetByIdAsync(id, correlationId, cancellationToken);
            if (chunk == null)
            {
                return false;
            }
            
            var fileId = chunk.FileId;
            
            var result = await _repository.DeleteAsync(id, correlationId, cancellationToken);
            if (result)
            {
                // Chunk cache'ini sil
                var chunkCacheKey = GetChunkCacheKey(id);
                await _cacheService.RemoveAsync(chunkCacheKey, correlationId);
                
                // Dosya chunk'larının cache'ini sıfırla
                var fileCacheKey = GetFileChunksCacheKey(fileId);
                await _cacheService.RemoveAsync(fileCacheKey, correlationId);
            }
            
            return result;
        }

        /// <summary>
        /// Belirli bir dosyaya ait tüm chunk'ları getirir
        /// </summary>
        public async Task<IEnumerable<ChunkMetadata>> GetChunksByFileIdAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetFileChunksCacheKey(fileId);
            
            // Önce cache'e bak
            var chunks = await _cacheService.GetAsync<List<ChunkMetadata>>(cacheKey, correlationId);
            if (chunks != null)
            {
                _logger.LogInformation("Dosya chunk'ları cache'den alındı: {FileId}, CorrelationId: {CorrelationId}", fileId, correlationId);
                return chunks;
            }

            // Cache'de yoksa repository'den al
            var result = await _repository.GetChunksByFileIdAsync(fileId, correlationId, cancellationToken);
            var chunksList = result as List<ChunkMetadata> ?? new List<ChunkMetadata>(result);
            
            if (chunksList.Count > 0)
            {
                // Cache'e ekle
                await _cacheService.SetAsync(cacheKey, chunksList, _defaultExpiry, correlationId);
                
                // Her bir chunk'ı da kendi anahtarıyla cache'e ekle
                foreach (var chunk in chunksList)
                {
                    var chunkCacheKey = GetChunkCacheKey(chunk.Id);
                    await _cacheService.SetAsync(chunkCacheKey, chunk, _defaultExpiry, correlationId);
                }
            }

            return chunksList;
        }

        /// <summary>
        /// Belirli bir storage provider'a ait tüm chunk'ları getirir
        /// </summary>
        public Task<IEnumerable<ChunkMetadata>> GetChunksByProviderIdAsync(string storageProviderId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            return _repository.GetChunksByProviderIdAsync(storageProviderId, correlationId, cancellationToken);
        }

        /// <summary>
        /// Belirli bir dosyaya ait tüm chunk'ları siler
        /// </summary>
        public async Task<bool> DeleteChunksByFileIdAsync(string fileId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // Önce chunk'ları getir (silmeden önce ID'lerini bilmemiz lazım)
            var chunks = await _repository.GetChunksByFileIdAsync(fileId, correlationId, cancellationToken);
            
            var result = await _repository.DeleteChunksByFileIdAsync(fileId, correlationId, cancellationToken);
            if (result)
            {
                // Dosya chunk'larının cache'ini sıfırla
                var fileCacheKey = GetFileChunksCacheKey(fileId);
                await _cacheService.RemoveAsync(fileCacheKey, correlationId);
                
                // Her bir chunk'ın cache'ini sil
                foreach (var chunk in chunks)
                {
                    var chunkCacheKey = GetChunkCacheKey(chunk.Id);
                    await _cacheService.RemoveAsync(chunkCacheKey, correlationId);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Chunk metaverisi cache anahtarını oluşturur
        /// </summary>
        private string GetChunkCacheKey(string id) => $"{CHUNK_METADATA_CACHE_KEY}{id}";
        
        /// <summary>
        /// Dosya chunk'larının cache anahtarını oluşturur
        /// </summary>
        private string GetFileChunksCacheKey(string fileId) => $"{FILE_CHUNKS_CACHE_KEY}{fileId}";
    }
}
