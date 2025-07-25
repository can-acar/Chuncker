using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Chuncker.Models;
using Chuncker.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Chuncker.Tests.Core
{
    /// <summary>
    /// Tests for file-related models and functionality
    /// </summary>
    public class FileModelTests
    {
        private readonly ITestOutputHelper _output;
        
        public FileModelTests(ITestOutputHelper output)
        {
            _output = output;
        }
        
        [Fact]
        public void FileMetadata_BasicPropertiesTest()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid();
            
            // Act
            var metadata = new FileMetadata
            {
                Id = fileId,
                Name = "test-file.txt",
                FullPath = "/path/to/test-file.txt",
                Size = 1024,
                FileSize = 1024, // Same as Size for compatibility
                Type = FileSystemObjectType.File,
                Extension = ".txt",
                ContentType = "text/plain",
                Status = FileStatus.Processing,
                ChunkCount = 3,
                CorrelationId = correlationId,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            
            // Assert
            Assert.Equal(fileId, metadata.Id);
            Assert.Equal("test-file.txt", metadata.Name);
            Assert.Equal("test-file.txt", metadata.FileName); // FileName is an alias for Name
            Assert.Equal("/path/to/test-file.txt", metadata.FullPath);
            Assert.Equal(1024, metadata.Size);
            Assert.Equal(1024, metadata.FileSize);
            Assert.Equal(FileSystemObjectType.File, metadata.Type);
            Assert.Equal(".txt", metadata.Extension);
            Assert.Equal("text/plain", metadata.ContentType);
            Assert.Equal(FileStatus.Processing, metadata.Status);
            Assert.Equal(3, metadata.ChunkCount);
            Assert.Equal(correlationId, metadata.CorrelationId);
            
            // Verify collections are initialized
            Assert.NotNull(metadata.Chunks);
            Assert.NotNull(metadata.Tags);
            Assert.NotNull(metadata.Children);
        }
        
        [Fact]
        public void ChunkMetadata_BasicPropertiesTest()
        {
            // Arrange
            var chunkId = Guid.NewGuid().ToString();
            var fileId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            
            // Act
            var metadata = new ChunkMetadata
            {
                Id = chunkId,
                FileId = fileId,
                SequenceNumber = 1,
                Size = 32768, // 32 KB
                CompressedSize = 16384, // 16 KB (50% compression)
                Checksum = "abc123def456",
                StorageProviderId = "filesystem",
                StoragePath = $"chunks/{fileId}/{chunkId}",
                IsCompressed = true,
                CreatedAt = createdAt,
                Status = "Stored",
                CorrelationId = correlationId
            };
            
            // Assert
            Assert.Equal(chunkId, metadata.Id);
            Assert.Equal(fileId, metadata.FileId);
            Assert.Equal(1, metadata.SequenceNumber);
            Assert.Equal(32768, metadata.Size);
            Assert.Equal(16384, metadata.CompressedSize);
            Assert.Equal("abc123def456", metadata.Checksum);
            Assert.Equal("filesystem", metadata.StorageProviderId);
            Assert.Equal($"chunks/{fileId}/{chunkId}", metadata.StoragePath);
            Assert.True(metadata.IsCompressed);
            Assert.Equal(createdAt, metadata.CreatedAt);
            Assert.Equal("Stored", metadata.Status);
            Assert.Equal(correlationId, metadata.CorrelationId);
        }
        
        [Fact]
        public void FileMetadata_BuildHierarchyTest()
        {
            // This test verifies that file metadata can correctly build a folder hierarchy
            
            // Arrange
            var rootFolder = new FileMetadata
            {
                Id = "root",
                Name = "root",
                Type = FileSystemObjectType.Directory,
                FullPath = "/root"
            };
            
            var subfolder1 = new FileMetadata
            {
                Id = "folder1",
                Name = "folder1",
                Type = FileSystemObjectType.Directory,
                FullPath = "/root/folder1",
                ParentId = "root"
            };
            
            var subfolder2 = new FileMetadata
            {
                Id = "folder2",
                Name = "folder2",
                Type = FileSystemObjectType.Directory,
                FullPath = "/root/folder2",
                ParentId = "root"
            };
            
            var file1 = new FileMetadata
            {
                Id = "file1",
                Name = "file1.txt",
                Type = FileSystemObjectType.File,
                FullPath = "/root/folder1/file1.txt",
                ParentId = "folder1",
                Size = 1024
            };
            
            var file2 = new FileMetadata
            {
                Id = "file2",
                Name = "file2.txt",
                Type = FileSystemObjectType.File,
                FullPath = "/root/folder2/file2.txt",
                ParentId = "folder2",
                Size = 2048
            };
            
            // Act
            // Add children to build the hierarchy
            rootFolder.Children.Add(subfolder1);
            rootFolder.Children.Add(subfolder2);
            subfolder1.Children.Add(file1);
            subfolder2.Children.Add(file2);
            
            // Assert
            // Verify the hierarchy structure
            Assert.Collection(rootFolder.Children,
                item => Assert.Equal("folder1", item.Id),
                item => Assert.Equal("folder2", item.Id));
            
            var folder1 = rootFolder.Children.Find(f => f.Id == "folder1");
            var folder2 = rootFolder.Children.Find(f => f.Id == "folder2");
            
            Assert.Single(folder1.Children);
            Assert.Single(folder2.Children);
            
            // Check file1 properties
            Assert.Collection(folder1.Children,
                file => {
                    Assert.Equal("file1.txt", file.Name);
                    Assert.Equal(1024, file.Size);
                });
                
            // Check file2 properties
            Assert.Collection(folder2.Children,
                file => {
                    Assert.Equal("file2.txt", file.Name);
                    Assert.Equal(2048, file.Size);
                });
        }
        
        [Theory]
        [InlineData(FileStatus.Pending, "Pending")]
        [InlineData(FileStatus.Processing, "Processing")]
        [InlineData(FileStatus.Completed, "Completed")]
        [InlineData(FileStatus.Error, "Error")]
        [InlineData(FileStatus.Failed, "Failed")]
        public void FileStatus_EnumValuesTest(FileStatus status, string expectedName)
        {
            // Assert
            Assert.Equal(expectedName, status.ToString());
        }
        
        [Fact]
        public void FileMetadata_CalculateChecksumTest()
        {
            // This test demonstrates how a file's checksum would be calculated
            
            // Arrange
            string testContent = "This is test file content";
            byte[] contentBytes = Encoding.UTF8.GetBytes(testContent);
            
            // Calculate expected SHA256 checksum
            string expectedChecksum;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(contentBytes);
                expectedChecksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            
            // Act
            var fileMetadata = new FileMetadata
            {
                Id = Guid.NewGuid().ToString(),
                Name = "checksum-test.txt",
                Type = FileSystemObjectType.File,
                Size = contentBytes.Length,
                Checksum = expectedChecksum
            };
            
            // Assert
            Assert.Equal(expectedChecksum, fileMetadata.Checksum);
            Assert.Equal(64, fileMetadata.Checksum.Length); // SHA-256 hash is 64 hex chars
        }
    }
}
