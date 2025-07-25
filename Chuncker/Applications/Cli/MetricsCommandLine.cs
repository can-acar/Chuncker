using Chuncker.Applications.Commands;
using Chuncker.Infsructures.Cli;
using Chuncker.Infsructures.Logging;
using Chuncker.Infsructures.Monitoring;
using Chuncker.Infsructures.UI;
using Chuncker.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Chuncker.Applications.Cli
{
    /// <summary>
    /// Metrics CLI komut işleyicisi
    /// </summary>
    public class MetricsCommandLine : CliCommandBase
    {
        private readonly IServiceProvider _serviceProvider;

        public MetricsCommandLine(IServiceProvider serviceProvider)
            : base("metrics", "Performans ve sistem metriklerini görüntüler")
        {
            _serviceProvider = serviceProvider;

            // Seçenekler
            AddOption(new CliOption<string>("type", "t", "Metrik tipi (memory, cpu, disk, all)", "all"));
            AddOption(new CliOption<bool>("detailed", "d", "Detaylı rapor", false));
            AddOption(new CliOption<int>("interval", "i", "Güncelleme aralığı (saniye)", 5));
        }

        /// <summary>
        /// Komut yürütme
        /// </summary>
        public override async Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var metricType = context.GetOptionValue<string>("type");
                var detailed = context.GetOptionValue<bool>("detailed");
                var interval = context.GetOptionValue<int>("interval");

                using var scope = LoggingContext.BeginCorrelationScope(Guid.NewGuid());
                var monitor = _serviceProvider.GetRequiredService<PerformanceMonitor>();

                ConsoleHelper.WriteInfo($"Performans metrikleri izleniyor ({metricType})...");
                ConsoleHelper.WriteInfo($"Güncelleme aralığı: {interval} saniye");
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.Clear();
                    ConsoleHelper.WriteHeader("Performans Metrikleri", 60);

                    switch (metricType.ToLower())
                    {
                        case "memory":
                            await ShowMemoryMetrics(monitor, detailed);
                            break;

                        case "cpu":
                            await ShowCpuMetrics(monitor, detailed);
                            break;

                        case "disk":
                            await ShowDiskMetrics(monitor, detailed);
                            break;

                        case "all":
                            await ShowMemoryMetrics(monitor, detailed);
                            await ShowCpuMetrics(monitor, detailed);
                            await ShowDiskMetrics(monitor, detailed);
                            break;

                        default:
                            ConsoleHelper.WriteError($"Geçersiz metrik tipi: {metricType}");
                            return 1;
                    }

                    Console.WriteLine("\nMetrikleri güncellemek için herhangi bir tuşa basın, çıkmak için 'q' tuşuna basın...");
                    
                    if (await WaitForKeyOrTimeout(interval, cancellationToken))
                    {
                        break; // Kullanıcı 'q' tuşuna bastı
                    }
                }

                return 0; // Başarılı
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Hata: {ex.Message}");
                return 1; // Başarısız
            }
        }

        private static async Task ShowMemoryMetrics(PerformanceMonitor monitor, bool detailed)
        {
            var metrics = await monitor.GetMemoryMetricsAsync();
            
            ConsoleHelper.WriteSubHeader("Bellek Kullanımı:", 40);
            ConsoleHelper.WriteLabelValue("Toplam Bellek", $"{metrics.TotalMemoryMB:N0} MB");
            ConsoleHelper.WriteLabelValue("Kullanılan Bellek", $"{metrics.UsedMemoryMB:N0} MB");
            ConsoleHelper.WriteLabelValue("Boş Bellek", $"{metrics.FreeMemoryMB:N0} MB");
            ConsoleHelper.WriteLabelValue("Bellek Kullanım Oranı", $"%{metrics.MemoryUsagePercent:N1}");

            if (detailed)
            {
                ConsoleHelper.WriteLabelValue("Page Size", $"{metrics.PageSizeKB:N0} KB");
                ConsoleHelper.WriteLabelValue("Virtual Memory", $"{metrics.VirtualMemoryMB:N0} MB");
                ConsoleHelper.WriteLabelValue("Cache Memory", $"{metrics.CacheMemoryMB:N0} MB");
            }
        }

        private static async Task ShowCpuMetrics(PerformanceMonitor monitor, bool detailed)
        {
            var metrics = await monitor.GetCpuMetricsAsync();
            
            ConsoleHelper.WriteSubHeader("CPU Kullanımı:", 40);
            ConsoleHelper.WriteLabelValue("CPU Kullanım Oranı", $"%{metrics.CpuUsagePercent:N1}");
            ConsoleHelper.WriteLabelValue("İşlemci Sayısı", metrics.ProcessorCount.ToString());
            ConsoleHelper.WriteLabelValue("Thread Sayısı", metrics.ThreadCount.ToString());

            if (detailed)
            {
                ConsoleHelper.WriteLabelValue("System CPU Time", metrics.SystemCpuTime.ToString());
                ConsoleHelper.WriteLabelValue("User CPU Time", metrics.UserCpuTime.ToString());
                ConsoleHelper.WriteLabelValue("Total CPU Time", metrics.TotalCpuTime.ToString());
            }
        }

        private static async Task ShowDiskMetrics(PerformanceMonitor monitor, bool detailed)
        {
            var metrics = await monitor.GetDiskMetricsAsync();
            
            ConsoleHelper.WriteSubHeader("Disk Kullanımı:", 40);
            ConsoleHelper.WriteLabelValue("Toplam Disk", $"{metrics.TotalSpaceGB:N1} GB");
            ConsoleHelper.WriteLabelValue("Kullanılan Disk", $"{metrics.UsedSpaceGB:N1} GB");
            ConsoleHelper.WriteLabelValue("Boş Disk", $"{metrics.FreeSpaceGB:N1} GB");
            ConsoleHelper.WriteLabelValue("Disk Kullanım Oranı", $"%{metrics.DiskUsagePercent:N1}");

            if (detailed)
            {
                ConsoleHelper.WriteLabelValue("Read Rate", $"{metrics.ReadRateMBps:N1} MB/s");
                ConsoleHelper.WriteLabelValue("Write Rate", $"{metrics.WriteRateMBps:N1} MB/s");
                ConsoleHelper.WriteLabelValue("IO Operations", metrics.IoOperationsPerSec.ToString());
            }
        }

        private static async Task<bool> WaitForKeyOrTimeout(int seconds, CancellationToken cancellationToken)
        {
            var delay = Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        return true;
                    }
                }

                if (await Task.WhenAny(delay, Task.Delay(100)) == delay)
                {
                    break;
                }
            }

            return false;
        }
    }
    }
