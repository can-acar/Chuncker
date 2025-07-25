using Chuncker.Applications.Commands;
using Chuncker.Infsructures.Cli;
using Chuncker.Infsructures.Logging;
using Chuncker.Infsructures.UI;
using Chuncker.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Chuncker.Applications.Cli
{
    /// <summary>
    /// Dosya indirme CLI komut işleyicisi
    /// </summary>
    public class DownloadCommandLine  : CliCommandBase
    {
        private readonly IServiceProvider _serviceProvider;

        public DownloadCommandLine(IServiceProvider serviceProvider)
            : base("download", "Bir dosyayı sistemden indirir")
        {
            _serviceProvider = serviceProvider;

            // Argümanlar
            AddArgument(new CliArgument<string>("fileId", "İndirilecek dosyanın ID'si", true));
            
            // Seçenekler
            AddOption(new CliOption<string>("output", "o", "Çıktı dosya yolu"));
        }

        /// <summary>
        /// Komut yürütme
        /// </summary>
        public override async Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var fileId = context.GetArgumentValue<string>("fileId");
                var outputPath = context.GetOptionValue<string>("output");
                
                using var scope = LoggingContext.BeginCorrelationScope(Guid.NewGuid());
                var commandMediator = _serviceProvider.GetRequiredService<ICommandMediator>();

                ConsoleHelper.WriteInfo($"Dosya indiriliyor: {fileId}");
                
                var downloadCommand = new DownloadFileCommand(fileId, outputPath);
                var success = await commandMediator.SendAsync<DownloadFileCommand, bool>(downloadCommand, cancellationToken);

                if (success)
                {
                    ConsoleHelper.WriteSuccess($"Dosya başarıyla indirildi: {outputPath}");
                    return 0; // Başarılı
                }
                else
                {
                    ConsoleHelper.WriteError($"Dosya indirme işlemi başarısız oldu: {fileId}");
                    return 1; // Başarısız
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Hata: {ex.Message}");
                return 1; // Başarısız
            }
        }
    }
}
