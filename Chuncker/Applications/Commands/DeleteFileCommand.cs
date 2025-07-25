using Chuncker.Interfaces;

namespace Chuncker.Applications.Commands
{
    /// <summary>
    /// Dosya silme komutu
    /// </summary>
    public class DeleteFileCommand : ICommand<bool>
    {
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Silinecek dosya kimliği
        /// </summary>
        public string FileId { get; set; }
        
        /// <summary>
        /// Zorla silme (uyarıları yoksay)
        /// </summary>
        public bool ForceDelete { get; set; } = false;
        
        /// <summary>
        /// İlişkili parçaların silinip silinmeyeceği
        /// </summary>
        public bool DeleteChunks { get; set; } = true;
        
        /// <summary>
        /// Silme nedeni (denetim amaçlı)
        /// </summary>
        public string DeletionReason { get; set; }

        public DeleteFileCommand(string fileId)
        {
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
        }

        public DeleteFileCommand(string fileId, string reason)
        {
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
            DeletionReason = reason;
        }
    }
}