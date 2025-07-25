using System;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Chuncker.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Http;
using Chuncker.Services;

namespace Chuncker
{
    /// <summary>
    /// Program başlatma için iyileştirmeler içeren yardımcı sınıf
    /// </summary>
    public static class Startup
    {
        /// <summary>
        /// Servis kayıtlarını optimize eder ve performans iyileştirmeleri yapar
        /// </summary>
        public static IServiceCollection OptimizeServices(this IServiceCollection services)
        {
            // Singleton servisler için ThreadLocal PooledObjectPolicy kullan
            services.AddSingleton<ObjectPoolProvider>(
                new DefaultObjectPoolProvider { MaximumRetained = 1024 });
            
            // HTTP istemci servisleri ekle
            services.AddHttpClient();
            
            // Bellek önbelleği ekle
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1024; // MB cinsinden
                options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
            });
            
            // HttpClient fabrikasını yapılandır
            services.AddHttpClient("ChunckerClient", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Chuncker/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            }).ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new System.Net.Http.SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                    MaxConnectionsPerServer = 20,
                    EnableMultipleHttp2Connections = true
                };
            });
            
            // Optimize edilmiş servisleri kullan
            services.AddTransient<IChunkManager, ChunkManager>();
            services.AddTransient<IFileService, FileService>();
            services.AddTransient<ICacheService, RedisCacheService>();
            
            return services;
        }
        
        /// <summary>
        /// Çalışma zamanı performans optimizasyonları
        /// </summary>
        public static void ConfigureRuntimeOptimizations()
        {
            // Thread havuzu ayarlarını optimize et
            int workerThreads = Environment.ProcessorCount * 4; // CPU çekirdek sayısının 4 katı
            int completionPortThreads = Environment.ProcessorCount * 8; // CPU çekirdek sayısının 8 katı
            ThreadPool.SetMinThreads(workerThreads, completionPortThreads);
            
            // GC ayarlarını optimize et
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        }
        
        /// <summary>
        /// Uygulama başlatmadan önce ön yükleme ve ısınma işlemleri
        /// </summary>
        public static async Task WarmupAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
        {
            // Repository bağlantılarını ısındır
            var fileRepo = services.GetRequiredService<IFileMetadataRepository>();
            var chunkRepo = services.GetRequiredService<IChunkMetadataRepository>();
            
            // Test bağlantıları (basit GetAllAsync çağrısı ile test edelim)
            try
            {
                await fileRepo.GetAllAsync(Guid.NewGuid());
                await chunkRepo.GetAllAsync(Guid.NewGuid());
            }
            catch (Exception)
            {
                // Bağlantı hatası varsa sessizce geç
            }
            
            // Redis bağlantısını ısındır
            var cacheService = services.GetRequiredService<ICacheService>();
            await cacheService.SetAsync("warmup_test", "test", TimeSpan.FromSeconds(5), Guid.NewGuid());
            
            // Storage provider'ları ısındır (sadece birkaç basit işlem)
            var providers = services.GetServices<IStorageProvider>();
            foreach (var provider in providers)
            {
                try
                {
                    // Basit bir test işlemi
                    await provider.ChunkExistsAsync("test_key", "test_path", Guid.NewGuid());
                }
                catch (Exception)
                {
                    // Hata varsa sessizce geç
                }
            }
        }
    }
}
