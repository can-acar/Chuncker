using Chuncker.Applications.Commands;
using Chuncker.Interfaces;
using Chuncker.Models;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.CommandHandlers
{
    /// <summary>
    /// Handler for list files commands
    /// </summary>
    public class ListFilesCommandHandler : ICommandHandler<ListFilesCommand, IEnumerable<FileMetadata>>
    {
        private readonly IFileService _fileService;
        private readonly ILogger<ListFilesCommandHandler> _logger;

        public ListFilesCommandHandler(
            IFileService fileService,
            ILogger<ListFilesCommandHandler> logger)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<FileMetadata>> HandleAsync(ListFilesCommand command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting file listing operation, CorrelationId: {CorrelationId}", command.CorrelationId);

                var files = await _fileService.ListFilesAsync(command.CorrelationId, cancellationToken);

                // Apply filters
                if (!string.IsNullOrEmpty(command.FileNamePattern))
                {
                    files = files.Where(f => f.FileName.Contains(command.FileNamePattern, StringComparison.OrdinalIgnoreCase));
                }

                if (command.StartDate.HasValue)
                {
                    files = files.Where(f => f.CreatedAt >= command.StartDate.Value);
                }

                if (command.EndDate.HasValue)
                {
                    files = files.Where(f => f.CreatedAt <= command.EndDate.Value);
                }

                if (command.Status.HasValue)
                {
                    files = files.Where(f => f.Status == command.Status.Value);
                }

                // Apply sorting
                files = command.SortBy?.ToLower() switch
                {
                    "filename" => command.SortDescending ? files.OrderByDescending(f => f.FileName) : files.OrderBy(f => f.FileName),
                    "filesize" => command.SortDescending ? files.OrderByDescending(f => f.FileSize) : files.OrderBy(f => f.FileSize),
                    "status" => command.SortDescending ? files.OrderByDescending(f => f.Status) : files.OrderBy(f => f.Status),
                    _ => command.SortDescending ? files.OrderByDescending(f => f.CreatedAt) : files.OrderBy(f => f.CreatedAt)
                };

                // Apply pagination
                if (command.Skip > 0)
                {
                    files = files.Skip(command.Skip);
                }

                if (command.Limit.HasValue)
                {
                    files = files.Take(command.Limit.Value);
                }

                var result = files.ToList();

                _logger.LogInformation("File listing completed successfully: {FileCount} files found, CorrelationId: {CorrelationId}",
                    result.Count, command.CorrelationId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File listing failed, CorrelationId: {CorrelationId}", command.CorrelationId);
                throw;
            }
        }
    }
}