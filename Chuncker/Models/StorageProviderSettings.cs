namespace Chuncker.Models
{
    /// <summary>
    /// Storage provider ayarları için yapılandırma sınıfı
    /// </summary>
    public class StorageProviderSettings
    {
        /// <summary>
        /// Dosya sistemi storage provider'ı için kök dizin
        /// </summary>
        public string FileSystemPath { get; set; } = "./Storage/Files";

        /// <summary>
        /// MongoDB storage provider'ı için veritabanı adı
        /// </summary>
        public string MongoDBPath { get; set; } = "ChunckerStorage";

        /// <summary>
        /// Storage provider dağıtım stratejisi
        /// </summary>
        public string DistributionStrategy { get; set; } = "RoundRobin";
    }
}
