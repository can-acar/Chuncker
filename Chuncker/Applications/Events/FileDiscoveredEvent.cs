using Chuncker.Infsructures.Events;

namespace Chuncker.Applications.Events
{
    /// <summary>
    /// Dosya keşfedildiğinde ve işlendiğinde yayınlanan event
    /// </summary>
    public class FileDiscoveredEvent : EventBase
    {
        /// <summary>
        /// Dosya kimliği
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// Dosya yolu
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Dosya adı
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Dosya boyutu (bytes)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Dosya uzantısı
        /// </summary>
        public string Extension { get; set; }

        /// <summary>
        /// MIME type
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Dosya checksum'ı
        /// </summary>
        public string Checksum { get; set; }

        /// <summary>
        /// İçerik işleme yapıldı mı
        /// </summary>
        public bool WasProcessed { get; set; }

        /// <summary>
        /// Chunk sayısı (işlendiyse)
        /// </summary>
        public int ChunkCount { get; set; }

        /// <summary>
        /// Dosya durumu
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Ebeveyn dizin kimliği
        /// </summary>
        public string ParentId { get; set; }

        /// <summary>
        /// Dosya etiketleri
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// İşlem süresi (milliseconds)
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Analiz yapıldı mı
        /// </summary>
        public bool WasAnalyzed { get; set; }

        /// <summary>
        /// Daha önce indekslenmişti mi
        /// </summary>
        public bool WasAlreadyIndexed { get; set; }

        /// <summary>
        /// Hata mesajı (varsa)
        /// </summary>
        public string ErrorMessage { get; set; }

        public override string EventType => nameof(FileDiscoveredEvent);
    }
}