using Chuncker.Applications.Commands;
using Chuncker.Infsructures.Cli;
using Chuncker.Infsructures.Logging;
using Chuncker.Infsructures.UI;
using Chuncker.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Chuncker.Applications.Cli
{
    /// <summary>
    /// Dosya bütünlüğünü doğrulama CLI komut işleyicisi
    /// </summary>
    public class VerifyCommandLine : CliCommandBase
    {
        private readonly IServiceProvider _serviceProvider;

        public VerifyCommandLine(IServiceProvider serviceProvider)
            : base("verify", "Bir dosyanın bütünlüğünü doğrular")
        {
            _serviceProvider = serviceProvider;

            // Argümanlar
            AddArgument(new CliArgument<string>("fileId", "Doğrulanacak dosyanın ID'si", isRequired: true));
            
            // Seçenekler
            AddOption(new CliOption<bool>("deep", "d", "Detaylı doğrulama yapar (tüm parçaları kontrol eder)", false));
            AddOption(new CliOption<bool>("repair", "r", "Hata tespit edilirse otomatik onarır", false));
        }

        /// <summary>
        /// Komut yürütme
        /// </summary>
        public override async Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var fileId = context.GetArgumentValue<string>("fileId");
                var deep = context.GetOptionValue<bool>("deep");
                var repair = context.GetOptionValue<bool>("repair");

                using var scope = LoggingContext.BeginCorrelationScope(Guid.NewGuid());
                var commandMediator = _serviceProvider.GetRequiredService<ICommandMediator>();

                ConsoleHelper.WriteInfo($"Dosya bütünlüğü kontrol ediliyor: {fileId}");
                
                if (deep)
                {
                    ConsoleHelper.WriteInfo("Detaylı doğrulama yapılıyor (tüm parçalar kontrol edilecek)...");
                }
                
                if (repair)
                {
                    ConsoleHelper.WriteInfo("Otomatik onarım aktif. Bozuk parçalar tespit edilirse onarılacak...");
                }

                var verifyCommand = new VerifyFileCommand(fileId, deep, repair);
                var result = await commandMediator.SendAsync<VerifyFileCommand, bool>(verifyCommand, cancellationToken);

                if (result)
                {
                    ConsoleHelper.WriteSuccess("Dosya bütünlük kontrolü başarılı. Dosya sağlam.");
                    return 0;
                }
                else
                {
                    ConsoleHelper.WriteError("Dosya bütünlük kontrolü başarısız. Dosya bozuk veya eksik.");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Hata: {ex.Message}");
                return 1;
            }
        }
    }
}
