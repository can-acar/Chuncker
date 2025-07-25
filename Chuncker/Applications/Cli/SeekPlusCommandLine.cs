using Chuncker.Applications.Commands;
using Chuncker.Infsructures.Cli;
using Chuncker.Infsructures.Logging;
using Chuncker.Infsructures.UI;
using Chuncker.Interfaces;
using Chuncker.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Chuncker.Applications.Cli
{
    /// <summary>
    /// Gelişmiş dosya sistemi tarama CLI komut işleyicisi
    /// </summary>
    public class SeekPlusCommandLine : CliCommandBase
    {
        private readonly IServiceProvider _serviceProvider;

        public SeekPlusCommandLine(IServiceProvider serviceProvider)
            : base("seek-plus", "Gelişmiş dosya sistemi tarama ve içerik işleme")
        {
            _serviceProvider = serviceProvider;

            // Argümanlar
            AddArgument(new CliArgument<DirectoryInfo>("path", "Taranacak dizin yolu", true));
            
            // Seçenekler
            AddOption(new CliOption<bool>("recursive", "r", "Alt dizinleri de tarar", true));
            AddOption(new CliOption<bool>("process-content", "c", "Dosya içeriğini işle ve parçalara böl", false));
            AddOption(new CliOption<bool>("progress", "prog", "İlerleme bilgisi göster", false));
            AddOption(new CliOption<bool>("parallel", "par", "Dosyaları paralel olarak işle", false));
            AddOption(new CliOption<bool>("check-duplicates", "dup", "Duplicate dosyaları kontrol et", false));
        }

        /// <summary>
        /// Komut yürütme
        /// </summary>
        public override async Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var directory = context.GetArgumentValue<DirectoryInfo>("path");
                var recursive = context.GetOptionValue<bool>("recursive");
                var processContent = context.GetOptionValue<bool>("process-content");
                var showProgress = context.GetOptionValue<bool>("progress");
                var parallel = context.GetOptionValue<bool>("parallel");
                var checkDuplicates = context.GetOptionValue<bool>("check-duplicates");

                using var scope = LoggingContext.BeginCorrelationScope(Guid.NewGuid());
                var commandMediator = _serviceProvider.GetRequiredService<ICommandMediator>();

                if (directory == null || !directory.Exists)
                {
                    ConsoleHelper.WriteError($"Dizin bulunamadı: {directory?.FullName ?? "null"}");
                    return 1;
                }

                ConsoleHelper.WriteInfo($"Gelişmiş dizin tarama başlatılıyor: {directory.FullName}");
                if (recursive) ConsoleHelper.WriteInfo("✓ Recursive tarama aktif");
                if (processContent) ConsoleHelper.WriteInfo("✓ İçerik işleme aktif");
                if (parallel) ConsoleHelper.WriteInfo("✓ Paralel işleme aktif");
                if (checkDuplicates) ConsoleHelper.WriteInfo("✓ Duplicate kontrolü aktif");

                IProgressReporter progressReporter = null;
                if (showProgress)
                {
                    progressReporter = _serviceProvider.GetRequiredService<IProgressReporter>();
                }

                var scanCommand = new ScanDirectoryCommand(directory.FullName, recursive, processContent)
                {
                    UseParallelProcessing = parallel,
                    CheckDuplicates = checkDuplicates,
                    ShowProgress = showProgress,
                    ProgressReporter = progressReporter
                };

                var results = await commandMediator.SendAsync<ScanDirectoryCommand, List<FileMetadata>>(scanCommand, cancellationToken);

                ConsoleHelper.WriteSuccess($"Gelişmiş tarama tamamlandı!");
                ConsoleHelper.WriteSeparator(50);

                // Detaylı özet
                if (results.Any())
                {
                    var totalSize = results.Sum(f => f.FileSize);
                    var completedFiles = results.Count(f => f.Status == Models.FileStatus.Completed);
                    var failedFiles = results.Count(f => f.Status == Models.FileStatus.Failed);

                    ConsoleHelper.WriteLabelValue("Toplam dosya", results.Count.ToString());
                    ConsoleHelper.WriteLabelValue("Başarılı", completedFiles.ToString());
                    ConsoleHelper.WriteLabelValue("Başarısız", failedFiles.ToString());
                    ConsoleHelper.WriteLabelValue("Toplam boyut", FormatFileSize(totalSize));
                    ConsoleHelper.WriteLabelValue("Ortalama boyut", FormatFileSize(totalSize / Math.Max(1, results.Count)));

                    if (processContent)
                    {
                        var totalChunks = results.Sum(f => f.ChunkCount);
                        ConsoleHelper.WriteLabelValue("Toplam chunk", totalChunks.ToString());
                    }

                    // Dosya türü analizi
                    var fileTypes = results.GroupBy(f => Path.GetExtension(f.FileName).ToLower())
                                          .OrderByDescending(g => g.Count())
                                          .Take(5);

                    ConsoleHelper.WriteSubHeader("En çok bulunan dosya türleri:", 40);
                    foreach (var type in fileTypes)
                    {
                        var ext = string.IsNullOrEmpty(type.Key) ? "(uzantısız)" : type.Key;
                        ConsoleHelper.WriteLabelValue($"  {ext}", type.Count().ToString());
                    }
                    
                    return 0; // Başarılı
                }
                else
                {
                    ConsoleHelper.WriteWarning("Hiç dosya bulunamadı.");
                    return 0; // Başarılı ama dosya bulunamadı
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Hata: {ex.Message}");
                return 1; // Başarısız
            }
        }
        
        /// <summary>
        /// Dosya boyutunu okunaklı formata çevirir
        /// </summary>
        private static string FormatFileSize(long size)
        {
            if (size < 1024) return $"{size} B";
            if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
            if (size < 1024 * 1024 * 1024) return $"{size / (1024.0 * 1024):F1} MB";
            return $"{size / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
