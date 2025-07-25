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
    /// Dosya listeleme CLI komut işleyicisi
    /// </summary>
    public class ListCommandLine : CliCommandBase
    {
        private readonly IServiceProvider _serviceProvider;

        public ListCommandLine(IServiceProvider serviceProvider)
            : base("list", "Sistemdeki dosyaları listeler")
        {
            _serviceProvider = serviceProvider;

            // Seçenekler
            AddOption(new CliOption<string>("pattern", "p", "Dosya adı deseni"));
            AddOption(new CliOption<DateTime?>("start", "s", "Başlangıç tarihi"));
            AddOption(new CliOption<DateTime?>("end", "e", "Bitiş tarihi"));
            AddOption(new CliOption<string>("status", "st", "Dosya durumu"));
            AddOption(new CliOption<int?>("limit", "l", "Sonuç limiti"));
        }

        /// <summary>
        /// Komut yürütme
        /// </summary>
        public override async Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var pattern = context.GetOptionValue<string>("pattern");
                var startDate = context.GetOptionValue<DateTime?>("start");
                var endDate = context.GetOptionValue<DateTime?>("end");
                var statusStr = context.GetOptionValue<string>("status");
                var limit = context.GetOptionValue<int?>("limit");

                FileStatus? status = null;
                if (!string.IsNullOrEmpty(statusStr) && Enum.TryParse<FileStatus>(statusStr, true, out var parsedStatus))
                {
                    status = parsedStatus;
                }

                using var scope = LoggingContext.BeginCorrelationScope(Guid.NewGuid());
                var commandMediator = _serviceProvider.GetRequiredService<ICommandMediator>();

                ConsoleHelper.WriteInfo("Dosyalar listeleniyor...");

                var listCommand = new ListFilesCommand(pattern, startDate, endDate)
                {
                    Status = status,
                    Limit = limit
                };

                var files = await commandMediator.SendAsync<ListFilesCommand, IEnumerable<FileMetadata>>(listCommand, cancellationToken);
                var fileList = files.ToList();

                if (fileList.Count == 0)
                {
                    ConsoleHelper.WriteInfo("Hiç dosya bulunamadı.");
                    return 0;
                }

                ConsoleHelper.WriteSuccess($"{fileList.Count} dosya bulundu:");
                
                // Dosya tablosunu yazdır
                Console.WriteLine();
                ConsoleHelper.WriteTableHeader("ID", "Dosya Adı", "Boyut", "Parça Sayısı", "Oluşturma Tarihi");
                
                foreach (var file in fileList)
                {
                    ConsoleHelper.WriteTableRow(
                        file.Id, 
                        file.FileName,
                        $"{file.FileSize:N0} bytes",
                        file.ChunkCount.ToString(),
                        file.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    );
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
