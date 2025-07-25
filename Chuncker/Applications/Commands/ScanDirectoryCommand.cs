using Chuncker.Interfaces;
using Chuncker.Models;

namespace Chuncker.Applications.Commands
{
    /// <summary>
    /// Dizin tarama komutu
    /// </summary>
    public class ScanDirectoryCommand : ICommand<List<FileMetadata>>
    {
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Taranacak dizin yolu
        /// </summary>
        public string Path { get; set; }
        
        /// <summary>
        /// Alt dizinlerin özyinelemeli olarak taranıp taranılmayacağı
        /// </summary>
        public bool Recursive { get; set; } = true;
        
        /// <summary>
        /// Dosya içeriğinin işlenip işlenmeyeceği (parça oluştur)
        /// </summary>
        public bool ProcessContent { get; set; } = false;
        
        /// <summary>
        /// Paralel işleme kullanılıp kullanılmayacağı
        /// </summary>
        public bool UseParallelProcessing { get; set; } = false;
        
        /// <summary>
        /// Tarama ilerlemesini izlemek için ilerleme raporu (isteğe bağlı)
        /// </summary>
        public IProgressReporter ProgressReporter { get; set; }
        
        /// <summary>
        /// İlerleme bilgisinin gösterilip gösterilmeyeceği
        /// </summary>
        public bool ShowProgress { get; set; } = false;
        
        /// <summary>
        /// KB cinsinden parça boyutu (0 = otomatik)
        /// </summary>
        public int ChunkSizeKB { get; set; } = 0;
        
        /// <summary>
        /// Kopya dosyaların kontrol edilip edilmeyeceği
        /// </summary>
        public bool CheckDuplicates { get; set; } = false;
        
        /// <summary>
        /// Dosya içeriğinin analiz edilip edilmeyeceği
        /// </summary>
        public bool AnalyzeContent { get; set; } = false;

        public ScanDirectoryCommand(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public ScanDirectoryCommand(string path, bool recursive, bool processContent)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Recursive = recursive;
            ProcessContent = processContent;
        }
    }
}