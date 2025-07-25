using Chuncker.Infsructures.Events;

namespace Chuncker.Applications.Events
{
    /// <summary>
    /// Dizin tarama işlemi tamamlandığında yayınlanan event
    /// </summary>
    public class DirectoryScanEvent : EventBase
    {
        /// <summary>
        /// Taranan dizin yolu
        /// </summary>
        public string DirectoryPath { get; set; }

        /// <summary>
        /// Bulunan dosya sayısı
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// Bulunan klasör sayısı
        /// </summary>
        public int DirectoryCount { get; set; }

        /// <summary>
        /// Toplam dosya boyutu (bytes)
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// İçerik işleme yapıldı mı
        /// </summary>
        public bool ProcessedContent { get; set; }

        /// <summary>
        /// Recursive tarama yapıldı mı
        /// </summary>
        public bool WasRecursive { get; set; }

        /// <summary>
        /// İşlem süresi (milliseconds)
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// İşlenen dosya sayısı (chunk'lara bölünen)
        /// </summary>
        public int ProcessedFileCount { get; set; }

        /// <summary>
        /// Toplam chunk sayısı
        /// </summary>
        public int TotalChunkCount { get; set; }

        /// <summary>
        /// Hata oluşan dosya sayısı
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Analiz edilen dosya sayısı
        /// </summary>
        public int AnalyzedFileCount { get; set; }

        public override string EventType => nameof(DirectoryScanEvent);
    }
}