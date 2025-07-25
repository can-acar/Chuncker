using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Chuncker.Infsructures.Monitoring
{
    /// <summary>
    /// Performance monitoring ve metrics collection sistemi
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly Dictionary<string, PerformanceMetric> _metrics;
        private readonly Timer _metricsTimer;
        private readonly object _lockObject = new object();

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger;
            _metrics = new Dictionary<string, PerformanceMetric>();
            
            // Her 30 saniyede bir metrics topla
            _metricsTimer = new Timer(CollectSystemMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// İşlem başlangıcını kaydet
        /// </summary>
        /// <param name="operationName">İşlem adı</param>
        /// <param name="correlationId">Correlation ID</param>
        /// <returns>Performance tracking activity</returns>
        public PerformanceActivity StartOperation(string operationName, Guid correlationId)
        {
            var activity = new PerformanceActivity(operationName, correlationId, this);
            
            _logger.LogDebug("İşlem başlatıldı: {OperationName}, CorrelationId: {CorrelationId}", 
                operationName, correlationId);
            
            return activity;
        }

        /// <summary>
        /// İşlem tamamlandığında metrikleri kaydet
        /// </summary>
        internal void RecordOperation(string operationName, TimeSpan duration, bool success, Guid correlationId)
        {
            lock (_lockObject)
            {
                if (!_metrics.TryGetValue(operationName, out var metric))
                {
                    metric = new PerformanceMetric(operationName);
                    _metrics[operationName] = metric;
                }

                metric.RecordExecution(duration, success);
            }

            _logger.LogInformation("İşlem tamamlandı: {OperationName}, Süre: {Duration}ms, Başarılı: {Success}, CorrelationId: {CorrelationId}",
                operationName, duration.TotalMilliseconds, success, correlationId);
        }

        /// <summary>
        /// Sistem metriklerini topla
        /// </summary>
        private void CollectSystemMetrics(object state)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                
                var memoryUsage = GC.GetTotalMemory(false);
                var workingSet = process.WorkingSet64;
                var cpuTime = process.TotalProcessorTime;
                
                lock (_lockObject)
                {
                    RecordSystemMetric("memory.gc_memory", memoryUsage);
                    RecordSystemMetric("memory.working_set", workingSet);
                    RecordSystemMetric("cpu.total_time_ms", cpuTime.TotalMilliseconds);
                    RecordSystemMetric("threads.count", process.Threads.Count);
                    RecordSystemMetric("handles.count", process.HandleCount);
                }

                _logger.LogDebug("Sistem metrikleri toplandı: Memory: {MemoryMB}MB, WorkingSet: {WorkingSetMB}MB, Threads: {ThreadCount}",
                    memoryUsage / 1024 / 1024, workingSet / 1024 / 1024, process.Threads.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sistem metrikleri toplanırken hata oluştu");
            }
        }
        
        /// <summary>
        /// Bellek metriklerini getir
        /// </summary>
        public async Task<MemoryMetrics> GetMemoryMetricsAsync()
        {
            return await Task.Run(() => {
                var process = Process.GetCurrentProcess();
                var memoryUsage = GC.GetTotalMemory(false);
                var workingSet = process.WorkingSet64;

                return new MemoryMetrics
                {
                    TotalMemoryMB = GetTotalSystemMemoryMB(),
                    UsedMemoryMB = workingSet / 1024 / 1024,
                    FreeMemoryMB = GetFreeSystemMemoryMB(),
                    MemoryUsagePercent = (double)workingSet / (GetTotalSystemMemoryMB() * 1024 * 1024) * 100,
                    PageSizeKB = GetSystemPageSizeKB(),
                    VirtualMemoryMB = process.VirtualMemorySize64 / 1024 / 1024,
                    CacheMemoryMB = memoryUsage / 1024 / 1024
                };
            });
        }

        /// <summary>
        /// CPU metriklerini getir
        /// </summary>
        public async Task<CpuMetrics> GetCpuMetricsAsync()
        {
            return await Task.Run(() => {
                var process = Process.GetCurrentProcess();
                
                return new CpuMetrics
                {
                    ProcessorCount = Environment.ProcessorCount,
                    ThreadCount = process.Threads.Count,
                    SystemCpuTime = process.PrivilegedProcessorTime,
                    UserCpuTime = process.UserProcessorTime,
                    TotalCpuTime = process.TotalProcessorTime,
                    CpuUsagePercent = GetCpuUsagePercentage()
                };
            });
        }

        /// <summary>
        /// Disk metriklerini getir
        /// </summary>
        public async Task<DiskMetrics> GetDiskMetricsAsync()
        {
            return await Task.Run(() => {
                var drive = DriveInfo.GetDrives().First(d => d.IsReady && d.Name == "/");
                var process = Process.GetCurrentProcess();

                return new DiskMetrics
                {
                    TotalSpaceGB = drive.TotalSize / 1024.0 / 1024.0 / 1024.0,
                    FreeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0,
                    UsedSpaceGB = (drive.TotalSize - drive.AvailableFreeSpace) / 1024.0 / 1024.0 / 1024.0,
                    DiskUsagePercent = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100,
                    ReadRateMBps = GetDiskReadRateMBps(),
                    WriteRateMBps = GetDiskWriteRateMBps(),
                    IoOperationsPerSec = GetIoOperationsPerSecond()
                };
            });
        }

        private double GetDiskReadRateMBps() => 0; // Implement using proper disk IO counters
        private double GetDiskWriteRateMBps() => 0; // Implement using proper disk IO counters
        private int GetIoOperationsPerSecond() => 0; // Implement using proper disk IO counters
        private double GetCpuUsagePercentage() => 0; // Implement using proper CPU counters

        private static long GetTotalSystemMemoryMB()
        {
            try
            {
                return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024;
            }
            catch
            {
                return 0;
            }
        }

        private static long GetFreeSystemMemoryMB()
        {
            try
            {
                var info = GC.GetGCMemoryInfo();
                return info.TotalAvailableMemoryBytes / 1024 / 1024;
            }
            catch
            {
                return 0;
            }
        }

        private static int GetSystemPageSizeKB()
        {
            try
            {
                return Environment.SystemPageSize / 1024;
            }
            catch
            {
                return 4; // Default 4KB
            }
        }

        /// <summary>
        /// Sistem metriğini kaydet
        /// </summary>
        private void RecordSystemMetric(string metricName, double value)
        {
            if (!_metrics.TryGetValue(metricName, out var metric))
            {
                metric = new PerformanceMetric(metricName);
                _metrics[metricName] = metric;
            }

            metric.RecordValue(value);
        }

        /// <summary>
        /// Tüm metrikleri getir
        /// </summary>
        public Dictionary<string, PerformanceMetricSummary> GetMetrics()
        {
            lock (_lockObject)
            {
                return _metrics.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.GetSummary()
                );
            }
        }

        /// <summary>
        /// Belirli bir metrik için detay getir
        /// </summary>
        public PerformanceMetricSummary GetMetric(string metricName)
        {
            lock (_lockObject)
            {
                return _metrics.TryGetValue(metricName, out var metric) 
                    ? metric.GetSummary() 
                    : null;
            }
        }

        /// <summary>
        /// Metrikleri temizle
        /// </summary>
        public void ResetMetrics()
        {
            lock (_lockObject)
            {
                _metrics.Clear();
            }
            
            _logger.LogInformation("Tüm performans metrikleri sıfırlandı");
        }

        public void Dispose()
        {
            _metricsTimer?.Dispose();
            
            // Son metrics raporunu yaz
            var finalMetrics = GetMetrics();
            _logger.LogInformation("Performance Monitor kapatılıyor. Toplam {MetricCount} metrik kaydedildi", 
                finalMetrics.Count);
        }
    }

    /// <summary>
    /// Performance tracking activity
    /// </summary>
    public class PerformanceActivity : IDisposable
    {
        private readonly string _operationName;
        private readonly Guid _correlationId;
        private readonly PerformanceMonitor _monitor;
        private readonly Stopwatch _stopwatch;
        private bool _success = true;
        private bool _disposed = false;

        internal PerformanceActivity(string operationName, Guid correlationId, PerformanceMonitor monitor)
        {
            _operationName = operationName;
            _correlationId = correlationId;
            _monitor = monitor;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// İşlemi başarısız olarak işaretle
        /// </summary>
        public void SetFailed()
        {
            _success = false;
        }

        /// <summary>
        /// İşlem için ek tag ekle
        /// </summary>
        public void AddTag(string key, object value)
        {
            // Activity source ile entegrasyon için
            Activity.Current?.SetTag(key, value?.ToString());
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _monitor.RecordOperation(_operationName, _stopwatch.Elapsed, _success, _correlationId);
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Performance metrik bilgisi
    /// </summary>
    internal class PerformanceMetric
    {
        private readonly string _name;
        private readonly List<double> _values;
        private readonly List<TimeSpan> _durations;
        private int _successCount;
        private int _failureCount;
        private DateTime _lastUpdate;

        public PerformanceMetric(string name)
        {
            _name = name;
            _values = new List<double>();
            _durations = new List<TimeSpan>();
            _lastUpdate = DateTime.UtcNow;
        }

        public void RecordExecution(TimeSpan duration, bool success)
        {
            _durations.Add(duration);
            
            if (success)
                _successCount++;
            else
                _failureCount++;
                
            _lastUpdate = DateTime.UtcNow;
        }

        public void RecordValue(double value)
        {
            _values.Add(value);
            _lastUpdate = DateTime.UtcNow;
        }

        public PerformanceMetricSummary GetSummary()
        {
            return new PerformanceMetricSummary
            {
                Name = _name,
                TotalExecutions = _successCount + _failureCount,
                SuccessCount = _successCount,
                FailureCount = _failureCount,
                SuccessRate = (_successCount + _failureCount) > 0 
                    ? (double)_successCount / (_successCount + _failureCount) * 100 
                    : 0,
                AverageDuration = _durations.Any() 
                    ? TimeSpan.FromMilliseconds(_durations.Average(d => d.TotalMilliseconds))
                    : TimeSpan.Zero,
                MinDuration = _durations.Any() ? _durations.Min() : TimeSpan.Zero,
                MaxDuration = _durations.Any() ? _durations.Max() : TimeSpan.Zero,
                AverageValue = _values.Any() ? _values.Average() : 0,
                MinValue = _values.Any() ? _values.Min() : 0,
                MaxValue = _values.Any() ? _values.Max() : 0,
                LastUpdate = _lastUpdate
            };
        }
    }

    /// <summary>
    /// Performance metrik özeti
    /// </summary>
    public class PerformanceMetricSummary
    {
        public string Name { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public double AverageValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Bellek metrikleri
    /// </summary>
    public class MemoryMetrics
    {
        public long TotalMemoryMB { get; set; }
        public long UsedMemoryMB { get; set; }
        public long FreeMemoryMB { get; set; }
        public double MemoryUsagePercent { get; set; }
        public int PageSizeKB { get; set; }
        public long VirtualMemoryMB { get; set; }
        public long CacheMemoryMB { get; set; }
    }

    /// <summary>
    /// CPU metrikleri
    /// </summary>
    public class CpuMetrics
    {
        public int ProcessorCount { get; set; }
        public int ThreadCount { get; set; }
        public TimeSpan SystemCpuTime { get; set; }
        public TimeSpan UserCpuTime { get; set; }
        public TimeSpan TotalCpuTime { get; set; }
        public double CpuUsagePercent { get; set; }
    }

    /// <summary>
    /// Disk metrikleri
    /// </summary>
    public class DiskMetrics
    {
        public double TotalSpaceGB { get; set; }
        public double UsedSpaceGB { get; set; }
        public double FreeSpaceGB { get; set; }
        public double DiskUsagePercent { get; set; }
        public double ReadRateMBps { get; set; }
        public double WriteRateMBps { get; set; }
        public int IoOperationsPerSec { get; set; }
    }
}