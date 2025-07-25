using Chuncker.Models;

namespace Chuncker.Interfaces
{
    /// <summary>
    /// Uzun süren işlemler için ilerleme raporlama arayüzü
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// İlerleme güncellemesi rapor eder
        /// </summary>
        /// <param name="progress">İlerleme bilgisi</param>
        void Report(ScanProgress progress);
        
        /// <summary>
        /// Bir işlemin başlangıcını asenkron olarak rapor eder
        /// </summary>
        /// <param name="operationId">İşlem tanımlayıcısı</param>
        /// <param name="operationDescription">İşlem açıklaması</param>
        /// <returns>Asenkron işlemi temsil eden görev</returns>
        Task ReportStartAsync(Guid operationId, string operationDescription);
        
        /// <summary>
        /// İlerlemeyi asenkron olarak rapor eder
        /// </summary>
        /// <param name="progress">İlerleme bilgisi</param>
        /// <returns>Asenkron işlemi temsil eden görev</returns>
        Task ReportProgressAsync(ScanProgress progress);
        
        /// <summary>
        /// Bir işlemin tamamlanmasını asenkron olarak rapor eder
        /// </summary>
        /// <param name="operationId">İşlem tanımlayıcısı</param>
        /// <param name="progress">Son ilerleme bilgisi</param>
        /// <returns>Asenkron işlemi temsil eden görev</returns>
        Task ReportCompletionAsync(Guid operationId, ScanProgress progress);
        
        /// <summary>
        /// Bir hatayı asenkron olarak rapor eder
        /// </summary>
        /// <param name="operationId">İşlem tanımlayıcısı</param>
        /// <param name="errorMessage">Hata mesajı</param>
        /// <returns>Asenkron işlemi temsil eden görev</returns>
        Task ReportErrorAsync(Guid operationId, string errorMessage);
    }
}
