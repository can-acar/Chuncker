
using System;
using System.Threading.Tasks;
using Chuncker.Models;
using System.Text;
using Chuncker.Interfaces;

namespace Chuncker.Services
{
    /// <summary>
    /// Console implementation of the IProgressReporter interface
    /// </summary>
    public class ConsoleProgressReporter : IProgressReporter
    {
        private readonly object _lockObject = new object();
        private readonly ConsoleColor _defaultForeground;
        private readonly ConsoleColor _defaultBackground;
        
        public ConsoleProgressReporter()
        {
            _defaultForeground = Console.ForegroundColor;
            _defaultBackground = Console.BackgroundColor;
        }
        
        /// <summary>
        /// Reports progress to the console
        /// </summary>
        public void Report(ScanProgress progress)
        {
            lock (_lockObject)
            {
                Console.WriteLine(FormatProgress(progress));
            }
        }
        
        /// <summary>
        /// Reports the start of an operation asynchronously
        /// </summary>
        public Task ReportStartAsync(Guid operationId, string operationDescription)
        {
            lock (_lockObject)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"▶ Starting operation: {operationDescription}");
                Console.WriteLine($"▶ Operation ID: {operationId}");
                Console.WriteLine(new string('-', 80));
                Console.ForegroundColor = _defaultForeground;
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Reports progress asynchronously
        /// </summary>
        public Task ReportProgressAsync(ScanProgress progress)
        {
            lock (_lockObject)
            {
                Console.Clear(); // Clear console for smoother updates
                Console.WriteLine(FormatProgress(progress));
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Reports completion of an operation asynchronously
        /// </summary>
        public Task ReportCompletionAsync(Guid operationId, ScanProgress progress)
        {
            lock (_lockObject)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"✓ Operation completed: {operationId}");
                Console.WriteLine($"✓ Total files processed: {progress.ProcessedFiles}");
                Console.WriteLine($"✓ Total bytes processed: {FormatSize(progress.ProcessedBytes)}");
                Console.WriteLine($"✓ Elapsed time: {progress.ElapsedTime}");
                Console.WriteLine($"✓ Average speed: {progress.FilesPerSecond:F2} files/s, {FormatSize((long)progress.BytesPerSecond)}/s");
                
                if (progress.Errors.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠ Completed with {progress.Errors.Count} errors/warnings.");
                }
                else
                {
                    Console.WriteLine("✓ No errors reported.");
                }
                
                Console.WriteLine(new string('-', 80));
                Console.ForegroundColor = _defaultForeground;
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Reports an error asynchronously
        /// </summary>
        public Task ReportErrorAsync(Guid operationId, string errorMessage)
        {
            lock (_lockObject)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"✗ Error in operation: {operationId}");
                Console.WriteLine($"✗ Error message: {errorMessage}");
                Console.WriteLine(new string('-', 80));
                Console.ForegroundColor = _defaultForeground;
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Formats the progress information into a string
        /// </summary>
        private string FormatProgress(ScanProgress progress)
        {
            var sb = new StringBuilder();
            
            // Format header based on status
            sb.AppendLine($"Status: {progress.Status}");
            
            if (!string.IsNullOrEmpty(progress.CurrentDirectory))
            {
                sb.AppendLine($"Current directory: {progress.CurrentDirectory}");
            }
            
            // Progress bar
            sb.Append("[");
            int progressBarLength = 50;
            int progressFilled = (int)(progress.ProgressPercentage / 100 * progressBarLength);
            
            for (int i = 0; i < progressBarLength; i++)
            {
                if (i < progressFilled)
                    sb.Append('█');
                else
                    sb.Append(' ');
            }
            
            sb.AppendLine($"] {progress.ProgressPercentage:F2}%");
            
            // Stats
            sb.AppendLine($"Files: {progress.ProcessedFiles}/{progress.TotalFiles} files");
            sb.AppendLine($"Size: {FormatSize(progress.ProcessedBytes)}/{FormatSize(progress.TotalBytes)}");
            sb.AppendLine($"Elapsed Time: {progress.ElapsedTime}");
            sb.AppendLine($"Speed: {progress.FilesPerSecond:F2} files/s, {FormatSize((long)progress.BytesPerSecond)}/s");
            
            // Errors if any
            if (progress.Errors.Count > 0)
            {
                sb.AppendLine("Errors:");
                int errorLimit = Math.Min(5, progress.Errors.Count); // Show at most 5 errors to avoid console clutter
                
                for (int i = 0; i < errorLimit; i++)
                {
                    sb.AppendLine($"- {progress.Errors[i]}");
                }
                
                if (progress.Errors.Count > errorLimit)
                {
                    sb.AppendLine($"... and {progress.Errors.Count - errorLimit} more errors");
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Formats a size in bytes to a human-readable string
        /// </summary>
        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
