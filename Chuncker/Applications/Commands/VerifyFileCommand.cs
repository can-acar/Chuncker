using Chuncker.Interfaces;

namespace Chuncker.Applications.Commands
{
    /// <summary>
    /// Dosya bütünlüğü doğrulama komutu
    /// </summary>
    public class VerifyFileCommand : ICommand<bool>
    {
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Doğrulanacak dosya kimliği
        /// </summary>
        public string FileId { get; set; }
        
        /// <summary>
        /// Derin doğrulama yapılıp yapılmayacağı (tüm parçaları kontrol et)
        /// </summary>
        public bool DeepVerification { get; set; } = true;
        
        /// <summary>
        /// Bozulma tespit edilirse dosyanın onarılıp onarılmayacağı
        /// </summary>
        public bool AutoRepair { get; set; } = false;
        
        /// <summary>
        /// Checksum doğrulaması yapılıp yapılmayacağı
        /// </summary>
        public bool VerifyChecksum { get; set; } = true;

        public VerifyFileCommand(string fileId)
        {
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
        }

        public VerifyFileCommand(string fileId, bool deepVerification, bool autoRepair = false)
        {
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
            DeepVerification = deepVerification;
            AutoRepair = autoRepair;
        }
    }
}