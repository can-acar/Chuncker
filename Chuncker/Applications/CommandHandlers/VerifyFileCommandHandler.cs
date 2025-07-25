using Chuncker.Applications.Commands;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.CommandHandlers
{
    /// <summary>
    /// Handler for file verification commands
    /// </summary>
    public class VerifyFileCommandHandler : ICommandHandler<VerifyFileCommand, bool>
    {
        private readonly IFileService _fileService;
        private readonly ILogger<VerifyFileCommandHandler> _logger;

        public VerifyFileCommandHandler(
            IFileService fileService,
            ILogger<VerifyFileCommandHandler> logger)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(VerifyFileCommand command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting file verification for: {FileId}, DeepVerification: {DeepVerification}, AutoRepair: {AutoRepair}, VerifyChecksum: {VerifyChecksum}, CorrelationId: {CorrelationId}",
                    command.FileId, command.DeepVerification, command.AutoRepair, command.VerifyChecksum, command.CorrelationId);

                var verificationResult = await _fileService.VerifyFileIntegrityAsync(
                    command.FileId,
                    command.CorrelationId,
                    cancellationToken);

                if (verificationResult)
                {
                    _logger.LogInformation("File verification completed successfully: {FileId}, CorrelationId: {CorrelationId}",
                        command.FileId, command.CorrelationId);
                }
                else
                {
                    _logger.LogWarning("File verification failed or corruption detected: {FileId}, CorrelationId: {CorrelationId}",
                        command.FileId, command.CorrelationId);
                }

                return verificationResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File verification failed for: {FileId}, CorrelationId: {CorrelationId}",
                    command.FileId, command.CorrelationId);
                throw;
            }
        }
    }
}