namespace Chuncker.Models
{
    /// <summary>
    /// Veritabanı ayarları için yapılandırma sınıfı
    /// </summary>
    public record DatabaseSettings
    {
        /// <summary>
        /// Veritabanı adı
        /// </summary>
        public string DatabaseName { get; set; } = "ChunckerDB";

        /// <summary>
        /// Chunk koleksiyonu adı
        /// </summary>
        public string ChunkCollectionName { get; set; } = "Chunks";

        /// <summary>
        /// Dosya metadata koleksiyonu adı
        /// </summary>
        public string FileMetadataCollectionName { get; set; } = "FileMetadata";

        /// <summary>
        /// Log koleksiyonu adı
        /// </summary>
        public string LogsCollectionName { get; set; } = "Logs";
    }
}
