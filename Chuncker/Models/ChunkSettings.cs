namespace Chuncker.Models
{
    /// <summary>
    /// Chunk ayarları için yapılandırma sınıfı
    /// </summary>
    public class ChunkSettings
    {
        /// <summary>
        /// Varsayılan chunk boyutu (byte cinsinden)
        /// </summary>
        public int DefaultChunkSizeInBytes { get; set; } = 1048576; // 1 MB

        /// <summary>
        /// Sıkıştırmanın etkinleştirilip etkinleştirilmediği
        /// </summary>
        public bool CompressionEnabled { get; set; } = true;

        /// <summary>
        /// Sıkıştırma seviyesi (0-9 arası)
        /// </summary>
        public int CompressionLevel { get; set; } = 6;

        /// <summary>
        /// Checksum algoritması
        /// </summary>
        public string ChecksumAlgorithm { get; set; } = "SHA256";

        /// <summary>
        /// Minimum chunk boyutu (byte cinsinden)
        /// </summary>
        public int MinChunkSizeInBytes { get; set; } = 65536; // 64 KB

        /// <summary>
        /// Maksimum chunk boyutu (byte cinsinden)
        /// </summary>
        public int MaxChunkSizeInBytes { get; set; } = 10485760; // 10 MB
    }
}
