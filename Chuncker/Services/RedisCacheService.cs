using System.Collections.Concurrent;
using System.Text.Json;
using Chuncker.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Chuncker.Services
{
    /// <summary>
    /// Redis önbellek servisi optimizasyonlu implementasyonu
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly ILogger<RedisCacheService> _logger;
        private readonly ConnectionMultiplexer _redisConnection;
        private readonly IDatabase _redisDatabase;
        private readonly TimeSpan _defaultExpiry;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks;
        
        // Batch işlemleri için
        private readonly TimeSpan _batchCooldownPeriod = TimeSpan.FromMilliseconds(50);
        private readonly int _batchMaxSize = 100;
        private readonly ConcurrentDictionary<string, BatchedOperation> _batchedOperations;

        public RedisCacheService(ILogger<RedisCacheService> logger, IConfiguration configuration)
        {
            _logger = logger;

            // Yapılandırma ayarlarını oku
            var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            var defaultExpiryMinutes = configuration["CacheSettings:DefaultExpiryInMinutes"] != null 
                ? int.Parse(configuration["CacheSettings:DefaultExpiryInMinutes"]) 
                : 30;
            _defaultExpiry = TimeSpan.FromMinutes(defaultExpiryMinutes);

            try
            {
                // Redis bağlantısını oluştur (Optimizasyonlar ile)
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 3;
                options.ConnectTimeout = 5000;
                options.SyncTimeout = 5000;
                
                _redisConnection = ConnectionMultiplexer.Connect(options);
                _redisDatabase = _redisConnection.GetDatabase();
                
                // Serializasyon ayarları
                _serializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };
                
                // Eş zamanlı erişim için semaphore koleksiyonu
                _keyLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
                
                // Batch işlemleri için sözlük
                _batchedOperations = new ConcurrentDictionary<string, BatchedOperation>();

                _logger.LogInformation("Redis önbellek servisi başlatıldı: {ConnectionString}", connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis önbellek servisi başlatılırken hata oluştu: {ConnectionString}", connectionString);
                throw;
            }
        }

        /// <summary>
        /// Önbellekten bir değeri getirir (Optimize edilmiş)
        /// </summary>
        public async Task<T> GetAsync<T>(string key, Guid correlationId) where T : class
        {
            // Anahtar kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                // Önbellekten değeri getir
                var cachedValue = await _redisDatabase.StringGetAsync(key);
                
                if (cachedValue.IsNull)
                {
                    _logger.LogDebug("Önbellekte değer bulunamadı: {Key}, CorrelationId: {CorrelationId}", key, correlationId);
                    return null;
                }
                
                // JSON'dan nesneye dönüştür
                var result = JsonSerializer.Deserialize<T>(cachedValue, _serializerOptions);
                
                _logger.LogDebug("Değer önbellekten getirildi: {Key}, CorrelationId: {CorrelationId}", key, correlationId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Önbellekten değer getirilirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", key, correlationId);
                return null; // Hata durumunda null dön
            }
        }
        
        /// <summary>
        /// Önbellekten bir değeri getirir (İç kullanım)
        /// </summary>
        private async Task<T> GetAsync<T>(string key, Guid correlationId, CancellationToken cancellationToken = default) where T : class
        {
            // Anahtar kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Önbellekten değeri getir
                var cachedValue = await _redisDatabase.StringGetAsync(key);
                
                if (cachedValue.IsNull)
                {
                    _logger.LogDebug("Önbellekte değer bulunamadı: {Key}, CorrelationId: {CorrelationId}", key, correlationId);
                    return null;
                }
                
                // JSON'dan nesneye dönüştür
                var result = JsonSerializer.Deserialize<T>(cachedValue, _serializerOptions);
                
                _logger.LogDebug("Değer önbellekten getirildi: {Key}, CorrelationId: {CorrelationId}", key, correlationId);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Önbellekten değer getirilirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", key, correlationId);
                return null; // Hata durumunda null dön
            }
        }

        /// <summary>
        /// Bir değeri önbelleğe ekler (Arayüz implementasyonu)
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry, Guid correlationId) where T : class
        {
            // Anahtar ve değer kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
                
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            // Geçerlilik süresi belirlenmemişse varsayılanı kullan
            TimeSpan actualExpiry = expiry ?? _defaultExpiry;

            // Eş zamanlı erişimi kontrol et
            var lockObj = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            
            try
            {
                await lockObj.WaitAsync();
                
                try
                {
                    // Değeri JSON'a dönüştür
                    var jsonValue = JsonSerializer.Serialize(value, _serializerOptions);
                    
                    // Redis'e ekle
                    await _redisDatabase.StringSetAsync(key, jsonValue, actualExpiry);
                    
                    _logger.LogDebug("Değer önbelleğe eklendi: {Key}, TTL: {Expiry}, CorrelationId: {CorrelationId}", 
                        key, actualExpiry, correlationId);
                }
                finally
                {
                    lockObj.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Değer önbelleğe eklenirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", 
                    key, correlationId);
                throw; // Arayüz implementasyonu hataları yukarı taşımalı
            }
        }
        
        /// <summary>
        /// Bir değeri önbelleğe ekler (İç kullanım için genişletilmiş versiyon)
        /// </summary>
        private async Task<bool> SetAsyncInternal<T>(string key, T value, TimeSpan? expiry = null, Guid correlationId = default, CancellationToken cancellationToken = default) where T : class
        {
            // Anahtar ve değer kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
                
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            // Geçerlilik süresi belirlenmemişse varsayılanı kullan
            if (expiry == null)
                expiry = _defaultExpiry;

            // Eş zamanlı erişimi kontrol et
            var lockObj = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            
            try
            {
                await lockObj.WaitAsync(cancellationToken);
                
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Değeri JSON'a dönüştür
                    var jsonValue = JsonSerializer.Serialize(value, _serializerOptions);
                    
                    // Redis'e ekle
                    var result = await _redisDatabase.StringSetAsync(key, jsonValue, expiry);
                    
                    _logger.LogDebug("Değer önbelleğe eklendi: {Key}, TTL: {Expiry}, CorrelationId: {CorrelationId}", 
                        key, expiry, correlationId);
                    
                    return result;
                }
                finally
                {
                    lockObj.Release();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Değer önbelleğe eklenirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", 
                    key, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Önbellekten bir değeri siler (ICacheService implementasyonu)
        /// </summary>
        Task<bool> ICacheService.RemoveAsync(string key, Guid correlationId)
        {
            // Anahtar kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                // Değeri sil
                return _redisDatabase.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Değer önbellekten silinirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", 
                    key, correlationId);
                return Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// Önbellekten bir değeri siler (Optimize edilmiş - İç kullanım)
        /// </summary>
        private async Task<bool> RemoveAsyncWithCancellation(string key, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // Anahtar kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Batch işlem için kuyruğa ekle
                return await EnqueueBatchOperationAsync(key, BatchOperationType.Remove, correlationId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Değer önbellekten silinirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", 
                    key, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Önbellekteki bir değerin geçerlilik süresini yeniler (ICacheService implementasyonu)
        /// </summary>
        Task<bool> ICacheService.RefreshExpiryAsync(string key, TimeSpan expiry, Guid correlationId)
        {
            // Anahtar kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                // Anahtarın var olup olmadığını kontrol et
                return _redisDatabase.KeyExpireAsync(key, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Değer geçerlilik süresi güncellenirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", 
                    key, correlationId);
                return Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// Önbellekteki bir değerin geçerlilik süresini yeniler (Optimize edilmiş - İç kullanım)
        /// </summary>
        private async Task<bool> RefreshExpiryAsyncInternal(string key, TimeSpan? expiry = null, Guid correlationId = default, CancellationToken cancellationToken = default)
        {
            // Anahtar kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            // Geçerlilik süresi belirlenmemişse varsayılanı kullan
            if (expiry == null)
                expiry = _defaultExpiry;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Anahtarın var olup olmadığını kontrol et
                if (!await _redisDatabase.KeyExistsAsync(key))
                {
                    _logger.LogDebug("Geçerlilik süresi güncellenecek anahtar bulunamadı: {Key}, CorrelationId: {CorrelationId}", 
                        key, correlationId);
                    return false;
                }
                
                // Geçerlilik süresini güncelle
                var result = await _redisDatabase.KeyExpireAsync(key, expiry);
                
                _logger.LogDebug("Değer geçerlilik süresi güncellendi: {Key}, TTL: {Expiry}, CorrelationId: {CorrelationId}", 
                    key, expiry, correlationId);
                
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Değer geçerlilik süresi güncellenirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", 
                    key, correlationId);
                return false;
            }
        }

        /// <summary>
        /// Önbellekte bir anahtarın var olup olmadığını kontrol eder (Optimize edilmiş)
        /// </summary>
        public async Task<bool> ExistsAsync(string key, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // Anahtar kontrolü
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Anahtarın var olup olmadığını kontrol et
                var result = await _redisDatabase.KeyExistsAsync(key);
                
                _logger.LogDebug("Anahtar kontrol edildi: {Key}, Var mı: {Exists}, CorrelationId: {CorrelationId}", 
                    key, result, correlationId);
                
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Anahtar kontrol edilirken hata oluştu: {Key}, CorrelationId: {CorrelationId}", 
                    key, correlationId);
                return false;
            }
        }
        
        /// <summary>
        /// İşlemi batch kuyruğuna ekler ve gerekirse işlemi başlatır
        /// </summary>
        private async Task<bool> EnqueueBatchOperationAsync(string key, BatchOperationType operationType, Guid correlationId, CancellationToken cancellationToken)
        {
            // Batch işlem grubu anahtarı (aynı türdeki işlemler için)
            var batchKey = $"{operationType}";
            
            // Mevcut batch işlemi al veya yeni oluştur
            var batchedOp = _batchedOperations.GetOrAdd(batchKey, _ => new BatchedOperation
            {
                Type = operationType,
                TaskSource = new TaskCompletionSource<bool>(),
                Keys = new ConcurrentBag<string>(),
                Timer = new Timer(_ => ProcessBatch(batchKey), null, _batchCooldownPeriod, Timeout.InfiniteTimeSpan)
            });
            
            // Anahtarı ekle
            batchedOp.Keys.Add(key);
            
            // Batch boyutu limitine ulaşıldıysa hemen işle
            if (batchedOp.Keys.Count >= _batchMaxSize)
            {
                ProcessBatch(batchKey);
            }
            
            return await batchedOp.TaskSource.Task;
        }
        
        /// <summary>
        /// Batch işlemi gerçekleştirir
        /// </summary>
        private void ProcessBatch(string batchKey)
        {
            // Batch işlemi al ve kaldır
            if (!_batchedOperations.TryRemove(batchKey, out var batchedOp))
            {
                return;
            }
            
            try
            {
                // Timer'ı durdur
                batchedOp.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
                batchedOp.Timer?.Dispose();
                
                // İşlem türüne göre batch gerçekleştir
                switch (batchedOp.Type)
                {
                    case BatchOperationType.Remove:
                        // Tüm anahtarları tek seferde sil
                        var batch = _redisDatabase.CreateBatch();
                        foreach (var key in batchedOp.Keys)
                        {
                            batch.KeyDeleteAsync(key);
                        }
                        batch.Execute();
                        break;
                }
                
                // İşlemi başarıyla tamamla
                batchedOp.TaskSource.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch işlemi gerçekleştirilirken hata oluştu: {BatchType}, {KeyCount} anahtar", 
                    batchedOp.Type, batchedOp.Keys.Count);
                    
                // Hatayı task'e bildir
                batchedOp.TaskSource.TrySetException(ex);
            }
        }
        
        /// <summary>
        /// Batch işlemi türleri
        /// </summary>
        private enum BatchOperationType
        {
            Remove,
            Expire
        }
        
        /// <summary>
        /// Batch işlemi bilgilerini tutan sınıf
        /// </summary>
        private class BatchedOperation
        {
            public BatchOperationType Type { get; set; }
            public TaskCompletionSource<bool> TaskSource { get; set; }
            public ConcurrentBag<string> Keys { get; set; }
            public Timer Timer { get; set; }
        }
        
        /// <summary>
        /// Kaynakları serbest bırakır
        /// </summary>
        public void Dispose()
        {
            // Redis bağlantısını kapat
            _redisConnection?.Dispose();
            
            // SemaphoreSlim nesnelerini temizle
            foreach (var lockObj in _keyLocks.Values)
            {
                lockObj.Dispose();
            }
            
            _keyLocks.Clear();
            
            // Timer'ları temizle
            foreach (var batchedOp in _batchedOperations.Values)
            {
                batchedOp.Timer?.Dispose();
            }
            
            _batchedOperations.Clear();
            
            _logger.LogInformation("Redis önbellek servisi kapatıldı");
        }
    }
}
