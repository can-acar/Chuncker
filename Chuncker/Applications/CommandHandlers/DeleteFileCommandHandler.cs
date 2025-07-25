using Chuncker.Applications.Commands;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.CommandHandlers
{
    /// <summary>
    /// Handler for file deletion commands
    /// </summary>
    public class DeleteFileCommandHandler : ICommandHandler<DeleteFileCommand, bool>
    {
        private readonly IFileService _fileService;
        private readonly ILogger<DeleteFileCommandHandler> _logger;

        public DeleteFileCommandHandler(
            IFileService fileService,
            ILogger<DeleteFileCommandHandler> logger)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(DeleteFileCommand command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting file deletion for: {FileId}, ForceDelete: {ForceDelete}, DeleteChunks: {DeleteChunks}, Reason: {Reason}, CorrelationId: {CorrelationId}",
                    command.FileId, command.ForceDelete, command.DeleteChunks, command.DeletionReason, command.CorrelationId);

                var deleteResult = await _fileService.DeleteFileAsync(
                    command.FileId,
                    command.CorrelationId,
                    cancellationToken);

                if (deleteResult)
                {
                    _logger.LogInformation("File deletion completed successfully: {FileId}, CorrelationId: {CorrelationId}",
                        command.FileId, command.CorrelationId);
                }
                else
                {
                    _logger.LogWarning("File deletion failed or file not found: {FileId}, CorrelationId: {CorrelationId}",
                        command.FileId, command.CorrelationId);
                }

                return deleteResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File deletion failed for: {FileId}, CorrelationId: {CorrelationId}",
                    command.FileId, command.CorrelationId);
                throw;
            }
        }
    }
}