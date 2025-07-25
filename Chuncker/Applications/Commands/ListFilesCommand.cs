using Chuncker.Interfaces;
using Chuncker.Models;

namespace Chuncker.Applications.Commands
{
    /// <summary>
    /// Dosya listeleme komutu
    /// </summary>
    public class ListFilesCommand : ICommand<IEnumerable<FileMetadata>>
    {
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Dosya adı desenine göre filtreleme (isteğe bağlı)
        /// </summary>
        public string FileNamePattern { get; set; }
        
        /// <summary>
        /// Tarih aralığı başlangıcına göre filtreleme (isteğe bağlı)
        /// </summary>
        public DateTime? StartDate { get; set; }
        
        /// <summary>
        /// Tarih aralığı bitisine göre filtreleme (isteğe bağlı)
        /// </summary>
        public DateTime? EndDate { get; set; }
        
        /// <summary>
        /// Dosya durumuna göre filtreleme (isteğe bağlı)
        /// </summary>
        public FileStatus? Status { get; set; }
        
        /// <summary>
        /// Maksimum sonuç sayısı (isteğe bağlı, varsayılan: sınır yok)
        /// </summary>
        public int? Limit { get; set; }
        
        /// <summary>
        /// Atlanacak sonuç sayısı (sayfalama için)
        /// </summary>
        public int Skip { get; set; } = 0;
        
        /// <summary>
        /// Sıralama alanı (isteğe bağlı)
        /// </summary>
        public string SortBy { get; set; } = "CreatedAt";
        
        /// <summary>
        /// Sıralama yönü (varsayılan olarak artan)
        /// </summary>
        public bool SortDescending { get; set; } = true;

        public ListFilesCommand()
        {
        }

        public ListFilesCommand(string fileNamePattern = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            FileNamePattern = fileNamePattern;
            StartDate = startDate;
            EndDate = endDate;
        }
    }
}