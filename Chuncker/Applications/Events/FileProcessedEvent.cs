using Chuncker.Infsructures.Events;

namespace Chuncker.Applications.Events
{
    /// <summary>
    /// Bir dosya işlendiğinde tetiklenen event
    /// </summary>
    public class FileProcessedEvent : EventBase
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public FileProcessedEvent() : base()
        {
            FileId = string.Empty;
            FileName = string.Empty;
            FileSize = 0;
            Checksum = string.Empty;
            ChunkCount = 0;
        }

        /// <summary>
        /// Yeni bir FileProcessedEvent oluşturur
        /// </summary>
        /// <param name="fileId">İşlenen dosyanın kimliği</param>
        /// <param name="fileName">İşlenen dosyanın adı</param>
        /// <param name="fileSize">İşlenen dosyanın boyutu</param>
        /// <param name="checksum">İşlenen dosyanın checksum değeri</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="chunkCount">İşlenen dosyanın parça sayısı</param>
        public FileProcessedEvent(string fileId, string fileName, long fileSize, string checksum, Guid correlationId, int chunkCount = 0)
            : base(correlationId)
        {
            FileId = fileId;
            FileName = fileName;
            FileSize = fileSize;
            Checksum = checksum;
            ChunkCount = chunkCount;
        }

        /// <summary>
        /// Event'in türü
        /// </summary>
        public override string EventType => "FileProcessed";

        /// <summary>
        /// İşlenen dosyanın kimliği
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// İşlenen dosyanın adı
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// İşlenen dosyanın boyutu
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// İşlenen dosyanın checksum değeri
        /// </summary>
        public string Checksum { get; set; }
        
        /// <summary>
        /// İşlenen dosyanın parça sayısı
        /// </summary>
        public int ChunkCount { get; set; }
    }
}
