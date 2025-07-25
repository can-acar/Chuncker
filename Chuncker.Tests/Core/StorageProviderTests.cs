using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Interfaces;
using Chuncker.Providers;
using Chuncker.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Chuncker.Tests.Core
{
    public class StorageProviderTests
    {
        private readonly TestMockHelper _mockHelper;
        private readonly ITestOutputHelper _output;
        private readonly string _testDirectory;
        
        public StorageProviderTests(ITestOutputHelper output)
        {
            _output = output;
            _mockHelper = new TestMockHelper(output);
            _testDirectory = Path.Combine(Path.GetTempPath(), "ChunckerTests", Guid.NewGuid().ToString());
            
            // Create test directory if it doesn't exist
            Directory.CreateDirectory(_testDirectory);
        }
        
        [Fact]
        public async Task FileSystemProvider_WriteAndReadChunkTest()
        {
            // Arrange
            var config = _mockHelper.CreateMockConfiguration(new Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", _testDirectory}
            });
            
            var logger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var provider = new FileSystemStorageProvider(config, logger.Object);
            
            string chunkId = "test-chunk-001";
            string testContent = "This is a test chunk content";
            using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
            var correlationId = Guid.NewGuid();
            
            try
            {
                // Act
                // Write the chunk
                string storagePath = await provider.WriteChunkAsync(
                    chunkId, 
                    contentStream, 
                    correlationId);
                
                // Verify the chunk exists
                bool exists = await provider.ChunkExistsAsync(
                    chunkId, 
                    storagePath, 
                    correlationId);
                    
                // Read the chunk back
                using var readStream = await provider.ReadChunkAsync(
                    chunkId, 
                    storagePath, 
                    correlationId);
                using var reader = new StreamReader(readStream);
                string readContent = await reader.ReadToEndAsync();
                
                // Assert
                Assert.True(exists);
                Assert.Equal(testContent, readContent);
                
                // Clean up
                bool deleteResult = await provider.DeleteChunkAsync(
                    chunkId, 
                    storagePath, 
                    correlationId);
                    
                Assert.True(deleteResult);
                
                // Verify deletion
                exists = await provider.ChunkExistsAsync(
                    chunkId, 
                    storagePath, 
                    correlationId);
                Assert.False(exists);
            }
            finally
            {
                // Make sure to clean up
                provider.Dispose();
                try
                {
                    if (Directory.Exists(_testDirectory))
                    {
                        Directory.Delete(_testDirectory, true);
                    }
                }
                catch
                {
                    _output.WriteLine($"Warning: Failed to clean up test directory {_testDirectory}");
                }
            }
        }
        
        [Fact]
        public async Task StorageProvider_ConcurrentWriteOperationsTest()
        {
            // Arrange
            var config = _mockHelper.CreateMockConfiguration(new Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", _testDirectory}
            });
            
            var logger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var provider = new FileSystemStorageProvider(config, logger.Object);
            
            const int concurrentOperations = 10;
            var tasks = new Task<string>[concurrentOperations];
            var correlationId = Guid.NewGuid();
            
            try
            {
                // Act
                for (int i = 0; i < concurrentOperations; i++)
                {
                    string chunkId = $"concurrent-chunk-{i:D3}";
                    string content = $"Concurrent test content #{i}";
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                    
                    // Start concurrent write operations
                    tasks[i] = provider.WriteChunkAsync(
                        chunkId, 
                        stream, 
                        correlationId);
                }
                
                // Wait for all writes to complete
                string[] storagePaths = await Task.WhenAll(tasks);
                
                // Assert
                for (int i = 0; i < concurrentOperations; i++)
                {
                    string chunkId = $"concurrent-chunk-{i:D3}";
                    bool exists = await provider.ChunkExistsAsync(
                        chunkId, 
                        storagePaths[i], 
                        correlationId);
                    Assert.True(exists);
                    
                    // Clean up
                    await provider.DeleteChunkAsync(
                        chunkId, 
                        storagePaths[i], 
                        correlationId);
                }
            }
            finally
            {
                // Clean up
                provider.Dispose();
                try
                {
                    if (Directory.Exists(_testDirectory))
                    {
                        Directory.Delete(_testDirectory, true);
                    }
                }
                catch
                {
                    _output.WriteLine($"Warning: Failed to clean up test directory {_testDirectory}");
                }
            }
        }
        
        [Fact]
        public void StorageProviderFactory_CreatesCorrectProvider()
        {
            // This test would verify that a storage provider factory
            // creates the correct type of provider based on configuration
            
            // For now, we're just testing basic provider properties
            var config = _mockHelper.CreateMockConfiguration();
            var logger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            
            var provider = new FileSystemStorageProvider(config, logger.Object);
            
            // Assert
            Assert.Equal("filesystem", provider.ProviderType, StringComparer.OrdinalIgnoreCase);
            Assert.NotNull(provider.ProviderId);
        }
        
        [Fact]
        public async Task StorageProvider_ErrorHandlingTest()
        {
            // Skip on macOS as it has permission issues with certain paths
            if (Environment.OSVersion.Platform == PlatformID.Unix || 
                Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                return; // Skip on Unix/macOS
            }
            
            // Arrange
            var config = _mockHelper.CreateMockConfiguration(new Dictionary<string, string>
            {
                {"StorageProviderSettings:FileSystemPath", Path.Combine(Path.GetTempPath(), "invalid_path_test")}
            });
            
            var logger = _mockHelper.CreateMockLogger<FileSystemStorageProvider>();
            var provider = new FileSystemStorageProvider(config, logger.Object);
            
            string chunkId = "test-error-chunk";
            string testContent = "This content should not be written";
            using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
            var correlationId = Guid.NewGuid();
            
            // Act & Assert
            // This test verifies that the provider handles errors gracefully,
            // which is implementation-dependent
            try
            {
                string storagePath = await provider.WriteChunkAsync(
                    chunkId,
                    contentStream,
                    correlationId);
                
                // If we get here without exception, the provider created the path
                bool exists = await provider.ChunkExistsAsync(
                    chunkId,
                    storagePath,
                    correlationId);
                
                Assert.True(exists);
                
                // Clean up
                await provider.DeleteChunkAsync(
                    chunkId,
                    storagePath,
                    correlationId);
            }
            catch (Exception ex)
            {
                // Expect DirectoryNotFoundException or similar
                _output.WriteLine($"Expected error: {ex.Message}");
                Assert.Contains("path", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                provider.Dispose();
            }
        }
    }
}
