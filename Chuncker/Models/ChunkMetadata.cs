using System;

namespace Chuncker.Models
{
    /// <summary>
    /// Bir dosya parçasının (chunk) metadata bilgilerini içeren sınıf.
    /// </summary>
    public class ChunkMetadata
    {
        /// <summary>
        /// Chunk'ın benzersiz kimliği
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Chunk'ın ait olduğu dosyanın kimliği
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// Chunk'ın dosya içindeki sıra numarası
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Chunk'ın boyutu (byte cinsinden)
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Chunk'ın sıkıştırılmış boyutu (byte cinsinden)
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Chunk'ın SHA256 checksum değeri
        /// </summary>
        public string Checksum { get; set; }

        /// <summary>
        /// Chunk'ın depolandığı storage provider kimliği
        /// </summary>
        public string StorageProviderId { get; set; }

        /// <summary>
        /// Chunk'ın depolandığı yol veya tanımlayıcı
        /// </summary>
        public string StoragePath { get; set; }

        /// <summary>
        /// Chunk'ın sıkıştırılıp sıkıştırılmadığı
        /// </summary>
        public bool IsCompressed { get; set; }

        /// <summary>
        /// Chunk'ın oluşturulma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Chunk'ın güncellenme tarihi
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Chunk'ın storage'a kaydedilme tarihi
        /// </summary>
        public DateTime? StorageTimestamp { get; set; }

        /// <summary>
        /// Chunk'a son erişim tarihi
        /// </summary>
        public DateTime? LastAccessTime { get; set; }

        /// <summary>
        /// Chunk'ın durumu (Processing, Stored, Error, etc.)
        /// </summary>
        public string Status { get; set; } = "Processing";

        /// <summary>
        /// İşlem izleme kimliği
        /// </summary>
        public Guid CorrelationId { get; set; }
    }
}
