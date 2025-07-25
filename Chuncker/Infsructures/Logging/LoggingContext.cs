using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;

namespace Chuncker.Infsructures.Logging
{
    /// <summary>
    /// Loglama ve CorrelationId takibi için yardımcı sınıf
    /// </summary>
    public static class LoggingContext
    {
        private const string CORRELATION_ID_PROPERTY = "CorrelationId";
        private static string _mongoConnectionString;
        private static string _databaseName;
        private static string _logCollectionName;
        private static AsyncLocal<Guid> CorrelationId = new AsyncLocal<Guid>();
        
        /// <summary>
        /// MongoDB log ayarlarını yapılandırır
        /// </summary>
        public static void ConfigureMongoDbLogging(IConfiguration configuration)
        {
            _mongoConnectionString = configuration.GetConnectionString("MongoDB");
            _databaseName = configuration.GetSection("DatabaseSettings:DatabaseName").Value ?? "ChunckerDB";
            _logCollectionName = configuration.GetSection("DatabaseSettings:LogCollectionName").Value ?? "Logs";
        }
        
        /// <summary>
        /// Verilen CorrelationId ile bir log kapsamı oluşturur
        /// </summary>
        /// <param name="correlationId">Log kapsamında kullanılacak correlationId</param>
        /// <returns>Dispose edildiğinde kapsamı temizleyen IDisposable</returns>
        public static IDisposable BeginCorrelationScope(Guid correlationId)
        {
            if (correlationId == Guid.Empty)
                throw new ArgumentException("CorrelationId cannot be empty", nameof(correlationId));
            // CorrelationId'yi sakla
            CorrelationId.Value = correlationId;
            // LogContext'e CorrelationId özelliğini ekle
            return new  CorrelationScope(LogContext.PushProperty(CORRELATION_ID_PROPERTY, correlationId));
        }
        
        /// <summary>
        /// Bir işlemi MongoDB'ye loglar
        /// </summary>
        /// <param name="operation">İşlem adı</param>
        /// <param name="message">Log mesajı</param>
        /// <param name="correlationId">İşlem korelasyon kimliği</param>
        public static void LogOperation(string operation, string message, Guid correlationId)
        {
            // Serilog aracılığıyla normal logla
            Log.Information("{Operation}: {Message}, CorrelationId: {CorrelationId}", 
                operation, message, correlationId);
            
            // MongoDB'ye doğrudan kaydet
            if (!string.IsNullOrEmpty(_mongoConnectionString))
            {
                LoggingConfiguration.LogOperationToMongo(
                    _mongoConnectionString, 
                    _databaseName, 
                    _logCollectionName,
                    operation,
                    message,
                    correlationId);
            }
        }
        
        /// <summary>
        /// Bir işlem başlangıcını MongoDB'ye loglar
        /// </summary>
        public static void LogOperationStart(string operation, Guid correlationId)
        {
            LogOperation(operation, $"{operation} işlemi başlatıldı", correlationId);
        }
        
        /// <summary>
        /// Bir işlem tamamlanmasını MongoDB'ye loglar
        /// </summary>
        public static void LogOperationComplete(string operation, Guid correlationId)
        {
            LogOperation(operation, $"{operation} işlemi tamamlandı", correlationId);
        }
        
        /// <summary>
        /// Bir işlem hatasını MongoDB'ye loglar
        /// </summary>
        public static void LogOperationError(string operation, string errorMessage, Guid correlationId)
        {
            LogOperation(operation, $"{operation} işleminde hata: {errorMessage}", correlationId);
        }


        public static Guid GetCorrelationId()
        {
            if (CorrelationId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("CorrelationId is not set. Please ensure to call BeginCorrelationScope first.");
            }

            return CorrelationId.Value;
        }
        
        class CorrelationScope : IDisposable
        {
            readonly IDisposable _logContextPop;

            public CorrelationScope(IDisposable logContextPop)
            {
                _logContextPop = logContextPop ?? throw new ArgumentNullException(nameof(logContextPop));
            }

            public void Dispose() => _logContextPop.Dispose();
        }
    }
}
