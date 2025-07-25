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
    /// Dosya sistemi tarama CLI komut işleyicisi
    /// </summary>
    public class SeekCommandLine : CliCommandBase
    {
        private readonly IServiceProvider _serviceProvider;

        public SeekCommandLine(IServiceProvider serviceProvider)
            : base("seek", "Dosya sistemini tarar")
        {
            _serviceProvider = serviceProvider;

            // Seçenekler
            AddOption(new CliOption<DirectoryInfo>("path", "p", "Taranacak dizin yolu"));
            AddOption(new CliOption<bool>("recursive", "r", "Alt dizinleri de tarar", true));
        }

        /// <summary>
        /// Komut yürütme
        /// </summary>
        public override async Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var pathOption = context.GetOptionValue<DirectoryInfo>("path");
                var directoryPath = pathOption?.FullName ?? Environment.CurrentDirectory;
                var recursive = context.GetOptionValue<bool>("recursive");

                var correlationId = Guid.NewGuid();
                using var scope = LoggingContext.BeginCorrelationScope(correlationId);
                var commandMediator = _serviceProvider.GetRequiredService<ICommandMediator>();
                
                // Klasör taraması için dizinin varlığını kontrol et
                var directory = new DirectoryInfo(directoryPath);
                if (!directory.Exists)
                {
                    // Eğer bu bir dizin değilse, dosya sisteminde depolanan dosyalarda arama yapalım
                    var fileService = _serviceProvider.GetRequiredService<IFileMetadataService>();
                    // FindByFileNameAsync kullanarak arama yapalım
                    var files = await fileService.FindByFileNameAsync(Path.GetFileName(directoryPath), LoggingContext.GetCorrelationId(), cancellationToken);
                    
                    if (files.Any())
                    {
                        ConsoleHelper.WriteSuccess($"Arama tamamlandı. {files.Count()} dosya bulundu.");
                        
                        Console.WriteLine();
                        ConsoleHelper.WriteTableHeader("ID", "Dosya Adı", "Boyut", "Parça Sayısı", "Oluşturma Tarihi");
                        
                        foreach (var file in files.Take(20))
                        {
                            ConsoleHelper.WriteTableRow(
                                file.Id,
                                file.FileName,
                                file.FileSize.ToString("N0") + " bytes",
                                file.ChunkCount.ToString(),
                                file.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                            );
                        }
                        
                        if (files.Count() > 20)
                        {
                            ConsoleHelper.WriteInfo($"...ve {files.Count() - 20} dosya daha");
                        }
                        
                        return 0;
                    }
                    else
                    {
                        ConsoleHelper.WriteError($"Dizin bulunamadı: {directory.FullName}");
                        return 1;
                    }
                }

                ConsoleHelper.WriteInfo($"Dizin tarama başlatılıyor: {directory.FullName}");
                if (recursive) ConsoleHelper.WriteInfo("Alt dizinler dahil taranacak");

                var scanCommand = new ScanDirectoryCommand(directory.FullName, recursive, false);

                // Use the command directly without going through the middleware pipeline
                var results = await commandMediator.SendAsync<ScanDirectoryCommand, List<FileMetadata>>(scanCommand, cancellationToken);

                ConsoleHelper.WriteSuccess($"Tarama tamamlandı. {results.Count} dosya bulundu.");

                if (results.Any())
                {
                    Console.WriteLine();
                    ConsoleHelper.WriteTableHeader("Dosya Adı", "Boyut", "Oluşturma Tarihi");
                    
                    foreach (var file in results.Take(20)) // İlk 20 sonuç
                    {
                        ConsoleHelper.WriteTableRow(
                            file.FileName,
                            file.FileSize.ToString("N0") + " bytes",
                            file.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                        );
                    }

                    if (results.Count > 20)
                    {
                        ConsoleHelper.WriteInfo($"...ve {results.Count - 20} dosya daha");
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
    }
}
