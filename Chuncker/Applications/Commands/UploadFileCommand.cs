using Chuncker.Interfaces;
using Chuncker.Models;

namespace Chuncker.Applications.Commands
{
    /// <summary>
    /// Dosya yükleme komutu
    /// </summary>
    public class UploadFileCommand : ICommand<FileMetadata>
    {
        public Guid CorrelationId { get; set; }
        
        /// <summary>
        /// Yüklenecek dosya akışı
        /// </summary>
        public Stream FileStream { get; set; }
        
        /// <summary>
        /// Orijinal dosya adı
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// Dosya boyutu (bayt cinsinden)
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// İçerik türü (isteğe bağlı)
        /// </summary>
        public string ContentType { get; set; }
        
        /// <summary>
        /// Ek metadata etiketleri
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// İçeriğin işlenip işlenmeyeceği (dosyayı parçalama)
        /// </summary>
        public bool ProcessContent { get; set; } = true;

        public UploadFileCommand(Stream fileStream, string fileName)
        {
            FileStream = fileStream ?? throw new ArgumentNullException(nameof(fileStream));
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            FileSize = fileStream.CanSeek ? fileStream.Length : 0;
        }
    }
}