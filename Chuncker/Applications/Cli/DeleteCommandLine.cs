using Chuncker.Applications.Commands;
using Chuncker.Infsructures.Cli;
using Chuncker.Infsructures.Logging;
using Chuncker.Infsructures.UI;
using Chuncker.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Chuncker.Applications.Cli
{
    /// <summary>
    /// Dosya silme CLI komut işleyicisi
    /// </summary>
    public class DeleteCommandLine : CliCommandBase
    {
        private readonly IServiceProvider _serviceProvider;

        public DeleteCommandLine(IServiceProvider serviceProvider)
            : base("delete", "Bir dosyayı sistemden siler")
        {
            _serviceProvider = serviceProvider;

            // Arguments
            AddArgument(new CliArgument<string>("fileId", "Silinecek dosyanın ID'si", isRequired: true));

            // Options
            AddOption(new CliOption<bool>("force", "f", "Zorla silme (onay sormadan)", false));
            AddOption(new CliOption<string>("reason", "r", "Silme nedeni (audit için)"));
            
        }

        /// <summary>
        /// Executes the delete command
        /// </summary>
        public override async Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var fileId = context.GetArgumentValue<string>("fileId");
                var force = context.GetOptionValue<bool>("force");
                var reason = context.GetOptionValue<string>("reason");

                using var scope = LoggingContext.BeginCorrelationScope(Guid.NewGuid());
                var commandMediator = _serviceProvider.GetRequiredService<ICommandMediator>();

                // Onay kontrolü
                if (!force)
                {
                    ConsoleHelper.WriteWarning($"'{fileId}' ID'li dosya silinecek. Bu işlem geri alınamaz!");
                    Console.Write("Devam etmek istiyor musunuz? (e/H): ");
                    var confirmation = Console.ReadLine()?.Trim().ToLower();
                    if (confirmation != "e" && confirmation != "evet" && confirmation != "yes" && confirmation != "y")
                    {
                        ConsoleHelper.WriteInfo("İşlem iptal edildi.");
                        return 0;
                    }
                }

                ConsoleHelper.WriteInfo($"Dosya siliniyor: {fileId}");

                var deleteCommand = new DeleteFileCommand(fileId, reason)
                {
                    ForceDelete = force,
                    DeleteChunks = true
                };

                var result = await commandMediator.SendAsync<DeleteFileCommand, bool>(deleteCommand, cancellationToken);

                if (result)
                {
                    ConsoleHelper.WriteSuccess("Dosya başarıyla silindi.");
                    return 0;
                }
                else
                {
                    ConsoleHelper.WriteWarning("Dosya silinemedi veya bulunamadı.");
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