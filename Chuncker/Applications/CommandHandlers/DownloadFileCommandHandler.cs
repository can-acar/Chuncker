using Chuncker.Applications.Commands;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.CommandHandlers
{
    /// <summary>
    /// Handler for file download commands
    /// </summary>
    public class DownloadFileCommandHandler : ICommandHandler<DownloadFileCommand, bool>
    {
        private readonly IFileService _fileService;
        private readonly ILogger<DownloadFileCommandHandler> _logger;

        public DownloadFileCommandHandler(
            IFileService fileService,
            ILogger<DownloadFileCommandHandler> logger)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(DownloadFileCommand command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting file download for: {FileId}, CorrelationId: {CorrelationId}",
                    command.FileId, command.CorrelationId);

                Stream outputStream = command.OutputStream;
                bool disposeStream = false;

                // If using file path, create file stream
                if (outputStream == null && !string.IsNullOrEmpty(command.OutputPath))
                {
                    // Check if file exists and handle overwrite
                    if (File.Exists(command.OutputPath) && !command.OverwriteExisting)
                    {
                        throw new InvalidOperationException($"File already exists: {command.OutputPath}. Use OverwriteExisting = true to overwrite.");
                    }

                    outputStream = new FileStream(command.OutputPath, FileMode.Create, FileAccess.Write);
                    disposeStream = true;
                }

                if (outputStream == null)
                {
                    throw new ArgumentException("Either OutputStream or OutputPath must be provided.");
                }

                try
                {
                    var downloadResult = await _fileService.DownloadFileAsync(
                        command.FileId,
                        outputStream,
                        command.CorrelationId,
                        cancellationToken);

                    _logger.LogInformation("File download completed: {FileId}, Success: {Success}, CorrelationId: {CorrelationId}",
                        command.FileId, downloadResult, command.CorrelationId);
                    
                    if (!downloadResult)
                    {
                        _logger.LogWarning("Dosya indirme işlemi başarısız oldu: {FileId}, CorrelationId: {CorrelationId}",
                            command.FileId, command.CorrelationId);
                    }

                    return downloadResult;
                }
                finally
                {
                    if (disposeStream)
                    {
                        outputStream.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File download failed for: {FileId}, CorrelationId: {CorrelationId}",
                    command.FileId, command.CorrelationId);
                throw;
            }
        }
    }
}