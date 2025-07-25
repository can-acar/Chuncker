using Chuncker.Interfaces;

namespace Chuncker.Applications.Commands
{
    /// <summary>
    /// Dosya indirme komutu
    /// </summary>
    public class DownloadFileCommand : ICommand<bool>
    {
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// İndirilecek dosya kimliği
        /// </summary>
        public string FileId { get; set; }
        
        /// <summary>
        /// Dosya içeriğinin yazılacağı çıktı akışı
        /// </summary>
        public Stream OutputStream { get; set; }
        
        /// <summary>
        /// Çıktı dosya yolu (OutputStream alternatifi)
        /// </summary>
        public string OutputPath { get; set; }
        
        /// <summary>
        /// İndirmeden sonra dosya bütünlüğünün doğrulanıp doğrulanmayacağı
        /// </summary>
        public bool VerifyIntegrity { get; set; } = true;
        
        /// <summary>
        /// Mevcut dosyanın üzerine yazılıp yazılmayacağı
        /// </summary>
        public bool OverwriteExisting { get; set; } = false;

        public DownloadFileCommand(string fileId, Stream outputStream)
        {
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
            OutputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        }

        public DownloadFileCommand(string fileId, string outputPath)
        {
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
            OutputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
        }
    }
}