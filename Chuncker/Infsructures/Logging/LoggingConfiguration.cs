using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace Chuncker.Infsructures.Logging
{
    /// <summary>
    /// Loglama altyapısını yapılandırmak için yardımcı sınıf
    /// </summary>
    public static class LoggingConfiguration
    {
        /// <summary>
        /// Serilog yapılandırmasını oluşturur ve IServiceCollection'a kaydeder
        /// </summary>
        /// <param name="services">Servis koleksiyonu</param>
        /// <param name="configuration">Uygulama yapılandırması</param>
        /// <returns>Güncellenen servis koleksiyonu</returns>
        public static IServiceCollection ConfigureLogging(this IServiceCollection services, IConfiguration configuration)
        {
            // MongoDB bağlantı bilgilerini al
            var mongoConnectionString = configuration.GetConnectionString("MongoDB");
            var databaseName = configuration.GetSection("DatabaseSettings:DatabaseName").Value ?? "ChunckerDB";
            var logCollectionName = configuration.GetSection("DatabaseSettings:LogCollectionName").Value ?? "Logs";

            // Log oluşturucusunu yapılandır
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Information() // Minimum log seviyesi
                .Enrich.FromLogContext() // Log bağlamından zenginleştirme
                .Enrich.WithMachineName() // Makine adıyla zenginleştirme
                .Enrich.WithThreadId() // Thread ID ile zenginleştirme
                .Enrich.WithProperty("Application", "Chuncker")
                .Enrich.WithProperty("Environment", "Production");

            // Konsol çıktısı ekle
            loggerConfig.WriteTo.Console();

            // Dosya çıktısı ekle
            loggerConfig.WriteTo.File(
                path: "logs/chuncker.log",
                rollingInterval: Serilog.RollingInterval.Day,
                retainedFileCountLimit: 31);

            // MongoDB doğrudan sink eklemesi için kaldırıldı - appsettings.json yapılandırması kullanılacak

            // Loki çıktısı ekle
            var lokiUri = configuration.GetConnectionString("Loki");
            if (!string.IsNullOrEmpty(lokiUri))
            {
                loggerConfig.WriteTo.GrafanaLoki(
                    lokiUri,
                    labels: new[] { 
                        new LokiLabel { Key = "app", Value = "chuncker" },
                        new LokiLabel { Key = "env", Value = "development" }
                    });
            }
            
            // Logger oluştur
            Log.Logger = loggerConfig.CreateLogger();
            
            // MongoDB log bağlantısını test et
            TestMongoDBLogConnection(mongoConnectionString, databaseName, logCollectionName);
            
            // ILogger için Serilog kullan
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });
            
            return services;
        }
        
        /// <summary>
        /// MongoDB log bağlantısını test eder
        /// </summary>
        private static void TestMongoDBLogConnection(string connectionString, string databaseName, string collectionName)
        {
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("MongoDB bağlantı dizesi bulunamadı, log testi yapılamadı!");
                    return;
                }
                
                var client = new MongoDB.Driver.MongoClient(connectionString);
                var database = client.GetDatabase(databaseName);
                var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                
                // Başlangıç log kaydı
                var testDoc = new MongoDB.Bson.BsonDocument
                {
                    { "Timestamp", DateTime.UtcNow },
                    { "Level", "Information" },
                    { "Message", "MongoDB log sistemi test edildi" },
                    { "Application", "Chuncker" },
                    { "Operation", "StartUp" },
                    { "Environment", "Production" },
                    { "ProcessId", System.Diagnostics.Process.GetCurrentProcess().Id },
                    { "MachineName", Environment.MachineName },
                    { "TestProperty", true }
                };
                
                collection.InsertOne(testDoc);
                Console.WriteLine($"MongoDB log bağlantısı test edildi: {databaseName}/{collectionName}");
                
                // MongoDB'ye işlem logları eklemek için statik bir yardımcı metot tanımlayalım
                Log.Information("MongoDB log sistemi hazır");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MongoDB log testi başarısız oldu: {ex.Message}");
            }
        }
        
        /// <summary>
        /// MongoDB'ye bir işlem log kaydı ekler
        /// </summary>
        public static void LogOperationToMongo(string connectionString, string databaseName, string collectionName, 
            string operation, string message, Guid correlationId)
        {
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                    return;
                
                var client = new MongoDB.Driver.MongoClient(connectionString);
                var database = client.GetDatabase(databaseName);
                var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                
                // İşlem log kaydı
                var logDoc = new MongoDB.Bson.BsonDocument
                {
                    { "Timestamp", DateTime.UtcNow },
                    { "Level", "Information" },
                    { "Message", message },
                    { "Application", "Chuncker" },
                    { "Operation", operation },
                    { "CorrelationId", correlationId.ToString() },
                    { "Environment", "Production" },
                    { "ProcessId", System.Diagnostics.Process.GetCurrentProcess().Id },
                    { "MachineName", Environment.MachineName }
                };
                
                collection.InsertOne(logDoc);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MongoDB işlem kaydı başarısız: {ex.Message}");
            }
        }        /// <summary>
        /// MongoDB'de log koleksiyonu için gerekli indeksleri oluşturur
        /// </summary>
        /// <param name="connectionString">MongoDB bağlantı dizesi</param>
        /// <param name="databaseName">Veritabanı adı</param>
        /// <param name="collectionName">Koleksiyon adı</param>
        public static void CreateLogIndexes(string connectionString, string databaseName, string collectionName)
        {
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("MongoDB bağlantı dizesi bulunamadı, log indeksleri oluşturulamadı!");
                    return;
                }

                var client = new MongoDB.Driver.MongoClient(connectionString);
                var database = client.GetDatabase(databaseName);
                var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);

                // Timestamp üzerinde indeks oluştur
                var timestampIndex = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Descending("Timestamp");
                collection.Indexes.CreateOne(new MongoDB.Driver.CreateIndexModel<MongoDB.Bson.BsonDocument>(
                    timestampIndex,
                    new MongoDB.Driver.CreateIndexOptions { Name = "IX_Timestamp" }));

                // Level üzerinde indeks oluştur
                var levelIndex = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("Level");
                collection.Indexes.CreateOne(new MongoDB.Driver.CreateIndexModel<MongoDB.Bson.BsonDocument>(
                    levelIndex,
                    new MongoDB.Driver.CreateIndexOptions { Name = "IX_Level" }));

                // CorrelationId üzerinde indeks oluştur
                var correlationIdIndex = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("Properties.CorrelationId");
                collection.Indexes.CreateOne(new MongoDB.Driver.CreateIndexModel<MongoDB.Bson.BsonDocument>(
                    correlationIdIndex,
                    new MongoDB.Driver.CreateIndexOptions { Name = "IX_CorrelationId" }));

                // TTL indeks oluştur - 30 gün sonra otomatik sil
                var ttlIndex = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("Timestamp");
                collection.Indexes.CreateOne(new MongoDB.Driver.CreateIndexModel<MongoDB.Bson.BsonDocument>(
                    ttlIndex,
                    new MongoDB.Driver.CreateIndexOptions
                    {
                        Name = "TTL_Timestamp",
                        ExpireAfter = TimeSpan.FromDays(30)
                    }));

                Console.WriteLine($"MongoDB log koleksiyonu indeksleri başarıyla oluşturuldu: {databaseName}/{collectionName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MongoDB log indeksleri oluşturulurken hata oluştu: {ex.Message}");
            }
        }
    }
}
