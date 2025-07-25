using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Providers;
using Chuncker.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Chuncker.Tests.Providers
{
    public class FileSystemStorageProviderTests : IDisposable
    {
        private readonly TestMockHelper _mockHelper;
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly string _testStoragePath;
        
        public FileSystemStorageProviderTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _mockHelper = new TestMockHelper(testOutputHelper);
            
            // Set up a unique test directory
            _testStoragePath = Path.Combine(Path.GetTempPath(), "ChunckerTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testStoragePath);
        }
        
        public void Dispose()
        {
            // Clean up test directory after tests
            try
            {
                if (Directory.Exists(_testStoragePath))
                {
                    Directory.Delete(_testStoragePath, true);
                }
            }
            catch (Exception ex)
            {
                _testOutputHelper.WriteLine($"Warning: Failed to clean up test directory: {ex.Message}");
            }
        }
        
        [Fact]
        public void Constructor_CreatesBaseDirectoryIfNotExists()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var testPath = Path.Combine(_testStoragePath, "CreateDirTest");
            
            // Ensure directory doesn't exist
            if (Directory.Exists(testPath))
            {
                Directory.Delete(testPath, true);
            }
            
            var configuration = _mockHelper.CreateMockConfiguration(new System.Collections.Generic.Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", testPath}
            });
            
            // Act
            var provider = new FileSystemStorageProvider(
                configuration,
                mockLogger.Object);
            
            // Assert
            Assert.True(Directory.Exists(testPath));
        }
        
        [Fact]
        public async Task WriteChunkAsync_WritesDataToFile()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var configuration = _mockHelper.CreateMockConfiguration(new System.Collections.Generic.Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", _testStoragePath}
            });
            
            var provider = new FileSystemStorageProvider(
                configuration,
                mockLogger.Object);
                
            string chunkId = "test-chunk-001";
            var testData = Encoding.UTF8.GetBytes("This is test chunk data for storage provider test");
            using var dataStream = new MemoryStream(testData);
            var correlationId = Guid.NewGuid();
            
            // Act
            var storagePath = await provider.WriteChunkAsync(
                chunkId, 
                dataStream, 
                correlationId);
            
            // Assert
            Assert.NotNull(storagePath);
            Assert.NotEmpty(storagePath);
            
            // Verify the file exists and has correct content
            string expectedFilePath = Path.Combine(_testStoragePath, storagePath);
            Assert.True(File.Exists(expectedFilePath), $"File should exist at {expectedFilePath}");
            
            var fileContent = await File.ReadAllBytesAsync(expectedFilePath);
            Assert.Equal(testData.Length, fileContent.Length);
            Assert.Equal(testData, fileContent);
        }
        
        [Fact]
        public async Task ReadChunkAsync_ReadsDataFromFile()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var configuration = _mockHelper.CreateMockConfiguration(new System.Collections.Generic.Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", _testStoragePath}
            });
            
            var provider = new FileSystemStorageProvider(
                configuration,
                mockLogger.Object);
                
            string chunkId = "test-chunk-002";
            var testData = Encoding.UTF8.GetBytes("This is test data for reading chunks");
            
            // First write the chunk using the provider
            var correlationId = Guid.NewGuid();
            using var dataStream = new MemoryStream(testData);
            
            // Act - First write the chunk
            string storagePath = await provider.WriteChunkAsync(
                chunkId, 
                dataStream, 
                correlationId);
                
            // Then read it back
            using var resultStream = await provider.ReadChunkAsync(
                chunkId, 
                storagePath, 
                correlationId);
                
            // Assert
            Assert.NotNull(resultStream);
            
            // Read the stream into a byte array
            using var memoryStream = new MemoryStream();
            await resultStream.CopyToAsync(memoryStream);
            var resultData = memoryStream.ToArray();
            
            // Compare the data
            Assert.Equal(testData.Length, resultData.Length);
            Assert.Equal(testData, resultData);
        }
        
        [Fact]
        public async Task DeleteChunkAsync_RemovesFileFromDisk()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var configuration = _mockHelper.CreateMockConfiguration(new System.Collections.Generic.Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", _testStoragePath}
            });
            
            var provider = new FileSystemStorageProvider(
                configuration,
                mockLogger.Object);
                
            string chunkId = "test-chunk-003";
            var testData = Encoding.UTF8.GetBytes("This is test data for deleting chunks");
            
            // First write the chunk using the provider
            var correlationId = Guid.NewGuid();
            using var dataStream = new MemoryStream(testData);
            
            // Write the chunk
            string storagePath = await provider.WriteChunkAsync(
                chunkId, 
                dataStream, 
                correlationId);
                
            // Verify file exists before delete
            Assert.True(File.Exists(storagePath));
            
            // Act - Delete the chunk
            var result = await provider.DeleteChunkAsync(
                chunkId, 
                storagePath, 
                correlationId);
                
            // Assert
            Assert.True(result);
            Assert.False(File.Exists(storagePath));
        }
        
        [Fact]
        public async Task ChunkExistsAsync_ReturnsTrueForExistingFile()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var configuration = _mockHelper.CreateMockConfiguration(new System.Collections.Generic.Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", _testStoragePath}
            });
            
            var provider = new FileSystemStorageProvider(
                configuration,
                mockLogger.Object);
                
            string chunkId = "test-chunk-004";
            var testData = Encoding.UTF8.GetBytes("This is test data for checking existence");
            
            // First write the chunk using the provider
            var correlationId = Guid.NewGuid();
            using var dataStream = new MemoryStream(testData);
            
            // Write the chunk
            string storagePath = await provider.WriteChunkAsync(
                chunkId, 
                dataStream, 
                correlationId);
            
            // Act - Check if the chunk exists
            var existsResult = await provider.ChunkExistsAsync(
                chunkId, 
                storagePath, 
                correlationId);
                
            // Assert
            Assert.True(existsResult);
            
            // Also test a non-existing file
            var nonExistsResult = await provider.ChunkExistsAsync(
                "non-existing-chunk", 
                "chunks/non-existing-chunk", 
                correlationId);
                
            Assert.False(nonExistsResult);
        }
        
        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var configuration = _mockHelper.CreateMockConfiguration(new System.Collections.Generic.Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", _testStoragePath}
            });
            
            var provider = new FileSystemStorageProvider(
                configuration,
                mockLogger.Object);
            
            // Act
            provider.Dispose();
            
            // Assert
            // Nothing to verify explicitly since there are no exposed resources
            // to check in the provider, but this at least ensures the method doesn't throw
        }
    }
}
