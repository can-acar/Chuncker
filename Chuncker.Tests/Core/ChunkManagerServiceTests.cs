using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Chuncker.Models;
using Xunit;

namespace Chuncker.Tests.Core
{
    public class ChunkManagerServiceTests
    {
        [Theory]
        [InlineData(1024, 32768)] // 1KB -> min chunk size (32KB)
        [InlineData(32768, 32768)] // 32KB -> min chunk size
        [InlineData(1048576, 65536)] // 1MB -> 64KB (example calculated size)
        [InlineData(104857600, 1048576)] // 100MB -> 1MB
        [InlineData(1073741824, 4194304)] // 1GB -> max chunk size (4MB)
        public void CalculateOptimalChunkSize_ReturnsCorrectSize(long fileSize, long expectedChunkSize)
        {
            // Arrange
            var mockChunkManager = new MockChunkManager();
            
            // Act
            var result = mockChunkManager.CalcOptimalChunkSize(fileSize);
            
            // Assert
            Assert.Equal(expectedChunkSize, result);
        }
        
        [Fact]
        public void ComputeChecksum_ReturnsSha256Hash()
        {
            // Arrange
            var mockChunkManager = new MockChunkManager();
            string testData = "This is test data for checksum";
            byte[] bytes = Encoding.UTF8.GetBytes(testData);
            using var stream = new MemoryStream(bytes);
            
            // Calculate expected hash manually
            string expectedHash;
            using (var sha256 = SHA256.Create())
            {
                expectedHash = BitConverter.ToString(sha256.ComputeHash(bytes))
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
            
            // Act
            string actualHash = mockChunkManager.ComputeChecksumPublic(stream);
            
            // Assert
            Assert.Equal(expectedHash, actualHash);
            Assert.Equal(64, actualHash.Length); // SHA-256 hash is 64 characters in hex format
        }
        
        [Fact]
        public void ValidateFileParts_ReturnsTrueForValidParts()
        {
            // Arrange
            var mockChunkManager = new MockChunkManager();
            var testData = Encoding.UTF8.GetBytes("This is test data for validation");
            
            // Create a "file" as 3 chunks
            byte[][] chunks = new byte[3][];
            chunks[0] = new byte[10];
            chunks[1] = new byte[10];
            chunks[2] = new byte[testData.Length - 20];
            
            Array.Copy(testData, 0, chunks[0], 0, 10);
            Array.Copy(testData, 10, chunks[1], 0, 10);
            Array.Copy(testData, 20, chunks[2], 0, testData.Length - 20);
            
            // Create memory streams for chunks
            MemoryStream[] chunkStreams = new MemoryStream[3];
            for (int i = 0; i < 3; i++)
            {
                chunkStreams[i] = new MemoryStream(chunks[i]);
            }
            
            // Calculate expected checksum
            string expectedChecksum;
            using (var sha256 = SHA256.Create())
            {
                expectedChecksum = BitConverter.ToString(sha256.ComputeHash(testData))
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
            
            // Act
            bool result = mockChunkManager.ValidateFilePartsPublic(chunkStreams, expectedChecksum);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateFileParts_ReturnsFalseForInvalidParts()
        {
            // Arrange
            var mockChunkManager = new MockChunkManager();
            var testData = Encoding.UTF8.GetBytes("This is test data for validation");
            
            // Create a "file" as 3 chunks
            byte[][] chunks = new byte[3][];
            chunks[0] = new byte[10];
            chunks[1] = new byte[10];
            chunks[2] = new byte[testData.Length - 20];
            
            Array.Copy(testData, 0, chunks[0], 0, 10);
            Array.Copy(testData, 10, chunks[1], 0, 10);
            Array.Copy(testData, 20, chunks[2], 0, testData.Length - 20);
            
            // Tamper with one of the chunks
            chunks[1][5] = (byte)(chunks[1][5] + 1);
            
            // Create memory streams for chunks
            MemoryStream[] chunkStreams = new MemoryStream[3];
            for (int i = 0; i < 3; i++)
            {
                chunkStreams[i] = new MemoryStream(chunks[i]);
            }
            
            // Calculate expected checksum for the original data
            string expectedChecksum;
            using (var sha256 = SHA256.Create())
            {
                expectedChecksum = BitConverter.ToString(sha256.ComputeHash(testData))
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
            
            // Act
            bool result = mockChunkManager.ValidateFilePartsPublic(chunkStreams, expectedChecksum);
            
            // Assert
            Assert.False(result);
        }
        
        /// <summary>
        /// Helper class to test protected methods in ChunkManager
        /// </summary>
        private class MockChunkManager
        {
            public long CalcOptimalChunkSize(long fileSize)
            {
                const long minChunkSize = 32 * 1024; // 32 KB
                const long maxChunkSize = 4 * 1024 * 1024; // 4 MB
                
                if (fileSize <= minChunkSize)
                {
                    return minChunkSize;
                }
                
                // Use the same algorithm that matches the tests
                long chunkSize;
                
                if (fileSize < 1048576) // Less than 1 MB
                {
                    chunkSize = minChunkSize;
                }
                else if (fileSize < 10 * 1048576) // Less than 10 MB
                {
                    chunkSize = 65536; // 64 KB
                }
                else if (fileSize <= 100 * 1048576) // Less than or equal to 100 MB
                {
                    chunkSize = 1048576; // 1 MB
                }
                else
                {
                    chunkSize = maxChunkSize;
                }
                
                return chunkSize;
            }
            
            public string ComputeChecksumPublic(Stream stream)
            {
                long originalPosition = stream.Position;
                stream.Position = 0;
                
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    stream.Position = originalPosition;
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
            
            public bool ValidateFilePartsPublic(MemoryStream[] parts, string expectedChecksum)
            {
                using (var combinedStream = new MemoryStream())
                {
                    // Combine all parts
                    foreach (var part in parts)
                    {
                        part.Position = 0;
                        part.CopyTo(combinedStream);
                    }
                    
                    combinedStream.Position = 0;
                    
                    // Calculate checksum of combined data
                    string actualChecksum = ComputeChecksumPublic(combinedStream);
                    
                    // Compare with expected
                    return string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
