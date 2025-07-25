using Chuncker.Infsructures.Events;

namespace Chuncker.Applications.Events
{
    /// <summary>
    /// Bir chunk depolandığında tetiklenen event
    /// </summary>
    public class ChunkStoredEvent : EventBase
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public ChunkStoredEvent() : base()
        {
            ChunkId = string.Empty;
            FileId = string.Empty;
            SequenceNumber = 0;
            ChunkSize = 0;
            CompressedSize = 0;
            Checksum = string.Empty;
            StorageProviderId = string.Empty;
        }

        /// <summary>
        /// Yeni bir ChunkStoredEvent oluşturur
        /// </summary>
        /// <param name="chunkId">Depolanan chunk'ın kimliği</param>
        /// <param name="fileId">Chunk'ın ait olduğu dosyanın kimliği</param>
        /// <param name="sequenceNumber">Chunk'ın sıra numarası</param>
        /// <param name="size">Chunk'ın boyutu</param>
        /// <param name="compressedSize">Chunk'ın sıkıştırılmış boyutu</param>
        /// <param name="checksum">Chunk'ın checksum değeri</param>
        /// <param name="storageProviderId">Chunk'ın depolandığı provider kimliği</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        public ChunkStoredEvent(
            string chunkId, 
            string fileId, 
            int sequenceNumber, 
            long size, 
            long compressedSize,
            string checksum,
            string storageProviderId,
            Guid correlationId)
            : base(correlationId)
        {
            ChunkId = chunkId;
            FileId = fileId;
            SequenceNumber = sequenceNumber;
            ChunkSize = size;
            CompressedSize = compressedSize;
            Checksum = checksum;
            StorageProviderId = storageProviderId;
        }

        /// <summary>
        /// Event'in türü
        /// </summary>
        public override string EventType => "ChunkStored";

        /// <summary>
        /// Depolanan chunk'ın kimliği
        /// </summary>
        public string ChunkId { get; set; }

        /// <summary>
        /// Chunk'ın ait olduğu dosyanın kimliği
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// Chunk'ın sıra numarası
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Chunk'ın boyutu
        /// </summary>
        public long ChunkSize { get; set; }

        /// <summary>
        /// Chunk'ın sıkıştırılmış boyutu
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Chunk'ın checksum değeri
        /// </summary>
        public string Checksum { get; set; }

        /// <summary>
        /// Chunk'ın depolandığı provider kimliği
        /// </summary>
        public string StorageProviderId { get; set; }
        public long Size { get; internal set; }
    }
}
