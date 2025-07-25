using Chuncker.Applications.Commands;
using Chuncker.Interfaces;
using Chuncker.Models;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.CommandHandlers
{
    /// <summary>
    /// Handler for file upload commands
    /// </summary>
    public class UploadFileCommandHandler : ICommandHandler<UploadFileCommand, FileMetadata>
    {
        private readonly IFileService _fileService;
        private readonly ILogger<UploadFileCommandHandler> _logger;

        public UploadFileCommandHandler(
            IFileService fileService,
            ILogger<UploadFileCommandHandler> logger)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FileMetadata> HandleAsync(UploadFileCommand command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting file upload process for: {FileName}, Size: {FileSize}, CorrelationId: {CorrelationId}",
                    command.FileName, command.FileSize, command.CorrelationId);

                var uploadResult = await _fileService.UploadFileAsync(
                    command.FileStream,
                    command.FileName,
                    command.CorrelationId,
                    cancellationToken);

                _logger.LogInformation("File upload completed successfully: {FileId}, CorrelationId: {CorrelationId}",
                    uploadResult.Id, command.CorrelationId);

                return uploadResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File upload failed for: {FileName}, CorrelationId: {CorrelationId}",
                    command.FileName, command.CorrelationId);
                throw;
            }
        }
    }
}