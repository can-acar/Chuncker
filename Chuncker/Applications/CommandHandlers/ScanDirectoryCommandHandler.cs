using Chuncker.Applications.Commands;
using Chuncker.Interfaces;
using Chuncker.Models;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.CommandHandlers
{
    /// <summary>
    /// Handler for directory scan commands
    /// </summary>
    public class ScanDirectoryCommandHandler : ICommandHandler<ScanDirectoryCommand, List<FileMetadata>>
    {
        private readonly IFileMetadataService _fileMetadataService;
        private readonly ILogger<ScanDirectoryCommandHandler> _logger;

        public ScanDirectoryCommandHandler(
            IFileMetadataService fileMetadataService,
            ILogger<ScanDirectoryCommandHandler> logger)
        {
            _fileMetadataService = fileMetadataService ?? throw new ArgumentNullException(nameof(fileMetadataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<FileMetadata>> HandleAsync(ScanDirectoryCommand command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting directory scan for: {Path}, Recursive: {Recursive}, ProcessContent: {ProcessContent}, CorrelationId: {CorrelationId}",
                    command.Path, command.Recursive, command.ProcessContent, command.CorrelationId);

                var scanResults = await _fileMetadataService.ScanDirectoryAsync(
                    command.Path,
                    command.Recursive,
                    command.CorrelationId,
                    cancellationToken);

                _logger.LogInformation("Directory scan completed successfully: {FileCount} files found, CorrelationId: {CorrelationId}",
                    scanResults.Count, command.CorrelationId);

                return scanResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Directory scan failed for: {Path}, CorrelationId: {CorrelationId}",
                    command.Path, command.CorrelationId);
                throw;
            }
        }
    }
}