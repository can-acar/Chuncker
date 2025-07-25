
using System;
using System.Collections.Generic;

namespace Chuncker.Models
{
    /// <summary>
    /// Scan progress tracking class with extended properties
    /// </summary>
    public class ScanProgress
    {
        public Guid OperationId { get; set; }
        public ScanProgressStatus Status { get; set; }
        public string CurrentDirectory { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public long TotalBytes { get; set; }
        public long ProcessedBytes { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public double FilesPerSecond { get; set; }
        public double BytesPerSecond { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public bool IsCompleted => Status == ScanProgressStatus.Completed;
        public long EstimatedTotal { get; set; }
        public long TotalProcessed => ProcessedFiles;
        public int ErrorCount => Errors.Count;

        public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;

        public void UpdateFileProcessed(string filePath, long fileSize, int chunkCount)
        {
            ProcessedFiles++;
            ProcessedBytes += fileSize;
        }

        public void UpdateDirectoryProcessed(string directoryPath)
        {
            // Update directory processed count or other metrics as needed
        }

        public void AddError(string errorMessage)
        {
            Errors.Add(errorMessage);
        }
    }

    /// <summary>
    /// Represents the status of a scan operation
    /// </summary>
    public enum ScanProgressStatus
    {
        NotStarted,
        Starting,
        Scanning,
        Processing,
        Completed,
        Failed,
        Canceled
    }
}
