using Chuncker.Applications.Commands;
using Chuncker.Infsructures.Cli;
using Chuncker.Infsructures.Logging;
using Chuncker.Infsructures.UI;
using Chuncker.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Chuncker.Applications.Cli
{
    /// <summary>
    /// Dosya yükleme CLI komut işleyicisi
    /// </summary>
    public class UploadCommandLine : CliCommandBase
    {
        private readonly IServiceProvider _serviceProvider;

        public UploadCommandLine(IServiceProvider serviceProvider)
            : base("upload", "Bir dosyayı sisteme yükler")
        {
            _serviceProvider = serviceProvider;

            // Argümanlar
            AddArgument(new CliArgument<FileInfo>("filePath", "Yüklenecek dosyanın yolu", true));
        }

        /// <summary>
        /// Komut yürütme
        /// </summary>
        public override async Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var file = context.GetArgumentValue<FileInfo>("filePath");
                
                using var scope = LoggingContext.BeginCorrelationScope(Guid.NewGuid());
                var commandMediator = _serviceProvider.GetRequiredService<ICommandMediator>();

                ConsoleHelper.WriteInfo($"Dosya yükleniyor: {file.FullName}");

                await using var fileStream = file.OpenRead();
                var uploadCommand = new UploadFileCommand(fileStream, file.Name)
                {
                    ProcessContent = true,
                    CorrelationId = LoggingContext.GetCorrelationId(),
                    
                }; 

                var result = await commandMediator.SendAsync<UploadFileCommand, Models.FileMetadata>(uploadCommand, cancellationToken);

                ConsoleHelper.WriteSuccess($"Dosya başarıyla yüklendi");
                ConsoleHelper.WriteSeparator(30);
                
                ConsoleHelper.WriteLabelValue("File Meta Data ID", result.Id);
                ConsoleHelper.WriteLabelValue("Dosya ID", result.Id);
                ConsoleHelper.WriteLabelValue("Dosya adı", result.FileName);
                ConsoleHelper.WriteLabelValue("Dosya boyutu", $"{result.FileSize:N0} bytes");
                ConsoleHelper.WriteLabelValue("Parça sayısı", result.ChunkCount.ToString());
                ConsoleHelper.WriteLabelValue("Korelasyon ID", uploadCommand.CorrelationId.ToString());
                
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
