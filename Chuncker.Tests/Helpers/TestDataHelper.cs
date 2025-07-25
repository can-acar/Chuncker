using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Chuncker.Models;

namespace Chuncker.Tests.Helpers
{
    /// <summary>
    /// Test verileri oluşturma ve yönetme yardımcısı
    /// </summary>
    public static class TestDataHelper
    {
        /// <summary>
        /// Belirli boyutta test verisi içeren bir Stream oluşturur
        /// </summary>
        /// <param name="sizeInKb">KB cinsinden veri boyutu</param>
        /// <returns>Test verisi içeren bir memory stream</returns>
        public static MemoryStream CreateTestStream(int sizeInKb)
        {
            // Test verisini oluştur (tekrarlanan pattern)
            byte[] data = new byte[sizeInKb * 1024];
            Random random = new Random(42); // Aynı test verileri için sabit seed
            random.NextBytes(data);
            
            return new MemoryStream(data);
        }
        
        /// <summary>
        /// Test amaçlı bir FileMetadata nesnesi oluşturur
        /// </summary>
        /// <param name="name">Dosya adı</param>
        /// <param name="sizeInKb">Dosya boyutu (KB)</param>
        /// <param name="chunkCount">Chunk sayısı</param>
        /// <returns>Oluşturulan FileMetadata nesnesi</returns>
        public static FileMetadata CreateTestFileMetadata(
            string name = "test-file.txt", 
            int sizeInKb = 100, 
            int chunkCount = 3)
        {
            return new FileMetadata
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                FileName = name,
                Size = sizeInKb * 1024,
                FileSize = sizeInKb * 1024,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                Extension = Path.GetExtension(name),
                Status = FileStatus.Processing,
                ChunkCount = chunkCount,
                CorrelationId = Guid.NewGuid(),
                Type = FileSystemObjectType.File,
                Checksum = ComputeMockChecksum(name + sizeInKb)
            };
        }
        
        /// <summary>
        /// Test amaçlı bir ChunkMetadata nesnesi oluşturur
        /// </summary>
        /// <param name="fileId">Dosya kimliği</param>
        /// <param name="sequenceNumber">Sıra numarası</param>
        /// <param name="sizeInKb">Chunk boyutu (KB)</param>
        /// <returns>Oluşturulan ChunkMetadata nesnesi</returns>
        public static ChunkMetadata CreateTestChunkMetadata(
            string fileId, 
            int sequenceNumber, 
            int sizeInKb = 32)
        {
            string chunkId = $"{fileId}_chunk_{sequenceNumber}";
            return new ChunkMetadata
            {
                Id = chunkId,
                FileId = fileId,
                SequenceNumber = sequenceNumber,
                Size = sizeInKb * 1024,
                CompressedSize = (sizeInKb * 1024) / 2, // Simulate compression
                Checksum = ComputeMockChecksum(chunkId),
                StorageProviderId = "filesystem",
                StoragePath = $"chunks/{fileId}/{chunkId}",
                IsCompressed = true,
                CreatedAt = DateTime.UtcNow,
                Status = "Stored",
                CorrelationId = Guid.NewGuid()
            };
        }
        
        /// <summary>
        /// Test amaçlı sabit bir checksum hesaplar
        /// </summary>
        /// <param name="input">Girdi metni</param>
        /// <returns>SHA256 checksum string</returns>
        public static string ComputeMockChecksum(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
        
        /// <summary>
        /// İki streamdeki verilerin aynı olup olmadığını kontrol eder
        /// </summary>
        /// <param name="stream1">Birinci stream</param>
        /// <param name="stream2">İkinci stream</param>
        /// <returns>Veriler aynıysa true</returns>
        public static bool CompareStreams(Stream stream1, Stream stream2)
        {
            if (stream1.Length != stream2.Length)
                return false;
                
            stream1.Position = 0;
            stream2.Position = 0;
            
            const int bufferSize = 4096;
            byte[] buffer1 = new byte[bufferSize];
            byte[] buffer2 = new byte[bufferSize];
            
            while (true)
            {
                int count1 = stream1.Read(buffer1, 0, bufferSize);
                int count2 = stream2.Read(buffer2, 0, bufferSize);
                
                if (count1 != count2)
                    return false;
                    
                if (count1 == 0)
                    return true;
                    
                for (int i = 0; i < count1; i++)
                {
                    if (buffer1[i] != buffer2[i])
                        return false;
                }
            }
        }
    }
}
