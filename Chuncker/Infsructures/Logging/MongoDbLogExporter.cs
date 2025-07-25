using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace Chuncker.Infsructures.Logging
{
    /// <summary>
    /// MongoDB'deki logları Loki tarafından kullanılmak üzere JSON dosyasına aktaran sınıf
    /// </summary>
    public class MongoDbLogExporter : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MongoDbLogExporter> _logger;
        private readonly string _exportPath;
        private readonly string _connectionString;
        private readonly string _databaseName;
        private readonly string _collectionName;
        private readonly TimeSpan _interval;
        private DateTime _lastExportTime;

        /// <summary>
        /// MongoDB log export edici oluşturur
        /// </summary>
        /// <param name="configuration">Uygulama yapılandırması</param>
        /// <param name="logger">Logger</param>
        public MongoDbLogExporter(IConfiguration configuration, ILogger<MongoDbLogExporter> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // MongoDB bağlantı ayarlarını al
            _connectionString = configuration.GetConnectionString("MongoDB");
            _databaseName = configuration.GetSection("DatabaseSettings:DatabaseName").Value ?? "ChunckerDB";
            _collectionName = configuration.GetSection("DatabaseSettings:LogCollectionName").Value ?? "Logs";
            
            // Export ayarlarını yapılandır
            _exportPath = configuration.GetSection("Logging:MongoDbExporter:ExportPath").Value ?? "./logs/mongodb-logs";
            
            // Varsayılan olarak 30 saniyede bir export yap
            var intervalSeconds = int.TryParse(
                configuration.GetSection("Logging:MongoDbExporter:IntervalSeconds").Value, 
                out var seconds) ? seconds : 30;
            _interval = TimeSpan.FromSeconds(intervalSeconds);
            
            // Son export zamanını şimdi olarak ayarla
            _lastExportTime = DateTime.UtcNow;
            
            // Export dizinini oluştur
            EnsureExportDirectoryExists();
        }

        /// <summary>
        /// Export dizininin varlığını kontrol eder ve gerekirse oluşturur
        /// </summary>
        private void EnsureExportDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_exportPath))
                {
                    Directory.CreateDirectory(_exportPath);
                    _logger.LogInformation("MongoDB log export dizini oluşturuldu: {ExportPath}", _exportPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export dizini oluşturulurken hata oluştu: {ExportPath}", _exportPath);
            }
        }

        /// <summary>
        /// Arka plan servisini çalıştırır
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MongoDB log export servisi başlatıldı, Export dizini: {ExportPath}, Aralık: {Interval} saniye", 
                _exportPath, _interval.TotalSeconds);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExportLogsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Log export işlemi sırasında hata oluştu");
                }
                
                await Task.Delay(_interval, stoppingToken);
            }
            
            _logger.LogInformation("MongoDB log export servisi durduruldu");
        }

        /// <summary>
        /// MongoDB'deki log kayıtlarını JSON dosyasına aktarır
        /// </summary>
        private async Task ExportLogsAsync()
        {
            try
            {
                // MongoDB'ye bağlan
                var client = new MongoClient(_connectionString);
                var database = client.GetDatabase(_databaseName);
                var collection = database.GetCollection<BsonDocument>(_collectionName);
                
                // Son export tarihinden sonraki kayıtları getir
                var filter = Builders<BsonDocument>.Filter.Gt("Timestamp", _lastExportTime);
                var logs = await collection.Find(filter).ToListAsync();
                
                if (logs.Count == 0)
                {
                    // Kayıt yoksa çık
                    return;
                }
                
                _logger.LogInformation("MongoDB'den {Count} log kaydı alındı", logs.Count);
                
                // Dosya adı oluştur: mongodb-logs-{timestamp}.json
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var filePath = Path.Combine(_exportPath, $"mongodb-logs-{timestamp}.json");
                
                // Logları JSON formatında dosyaya yaz
                using (var writer = new StreamWriter(filePath))
                {
                    foreach (var log in logs)
                    {
                        // Loki için uygun formata çevir - Promtail'in beklediği formata uygun olarak
                        var lokiLog = new
                        {
                            Timestamp = log.Contains("Timestamp") ? log["Timestamp"].ToUniversalTime().ToString("o") : DateTime.UtcNow.ToString("o"),
                            Level = log.Contains("Level") ? log["Level"].AsString : "Information",
                            RenderedMessage = log.Contains("Message") ? log["Message"].AsString : "No message",
                            Exception = log.Contains("Exception") ? log["Exception"].AsString : null,
                            SourceContext = log.Contains("SourceContext") ? log["SourceContext"].AsString : "Chuncker",
                            Properties = new
                            {
                                CorrelationId = log.Contains("CorrelationId") ? log["CorrelationId"].AsString : null,
                                Operation = log.Contains("Operation") ? log["Operation"].AsString : null,
                                Application = "Chuncker",
                                Environment = log.Contains("Environment") ? log["Environment"].AsString : "Production"
                            }
                        };
                        
                        // JSON'a çevir ve dosyaya yaz (her log ayrı bir satırda)
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(lokiLog));
                    }
                }
                
                _logger.LogInformation("MongoDB logları JSON dosyasına aktarıldı: {FilePath}", filePath);
                
                // En son kaydın tarihini al ve son export zamanını güncelle
                _lastExportTime = logs.Max(log => log.Contains("Timestamp") 
                    ? log["Timestamp"].ToUniversalTime() 
                    : DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoDB logları export edilirken hata oluştu");
            }
        }
    }
}
