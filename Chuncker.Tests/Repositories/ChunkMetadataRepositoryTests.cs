using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Models;
using Chuncker.Repositories;
using Chuncker.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Chuncker.Tests.Repositories
{
    public class ChunkMetadataRepositoryTests
    {
        private readonly TestMockHelper _mockHelper;
        private readonly ITestOutputHelper _testOutputHelper;
        
        public ChunkMetadataRepositoryTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _mockHelper = new TestMockHelper(testOutputHelper);
        }
        
        [Fact]
        public async Task AddAsync_AddsChunkMetadata()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<ChunkMetadataRepository>();
            
            // Mock MongoDB collection
            var mockCollection = new Mock<IMongoCollection<ChunkMetadata>>();
            mockCollection
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<ChunkMetadata>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
                
            // Create repository with mocked dependencies
            var repository = SetupRepositoryWithMockCollection(mockLogger.Object, mockCollection.Object);
            
            // Test data
            var chunk = TestDataHelper.CreateTestChunkMetadata(
                "test-file-001",
                1,
                1024);
                
            // Act
            var correlationId = Guid.NewGuid();
            var result = await repository.AddAsync(chunk, correlationId);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(chunk.Id, result.Id);
            mockCollection.Verify(
                c => c.InsertOneAsync(
                    It.Is<ChunkMetadata>(cm => cm.Id == chunk.Id),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task GetByIdAsync_ReturnsChunkMetadata()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<ChunkMetadataRepository>();
            
            // Create test data
            string chunkId = "test-chunk-001";
            var chunk = TestDataHelper.CreateTestChunkMetadata("test-file-001", 1, 1024);
            chunk.Id = chunkId;
            
            // Mock cursor
            var mockCursor = new Mock<IAsyncCursor<ChunkMetadata>>();
            mockCursor
                .Setup(c => c.Current)
                .Returns(new List<ChunkMetadata> { chunk });
            mockCursor
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
                
            // Mock collection
            var mockCollection = new Mock<IMongoCollection<ChunkMetadata>>();
            mockCollection
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<ChunkMetadata>>(),
                    It.IsAny<FindOptions<ChunkMetadata, ChunkMetadata>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCursor.Object);
                
            // Create repository
            var repository = SetupRepositoryWithMockCollection(mockLogger.Object, mockCollection.Object);
            
            // Act
            var correlationId = Guid.NewGuid();
            var result = await repository.GetByIdAsync(chunkId, correlationId);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(chunkId, result.Id);
        }
        
        [Fact]
        public async Task GetChunksByFileIdAsync_ReturnsChunks()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<ChunkMetadataRepository>();
            
            // Create test data
            string fileId = "test-file-001";
            var chunks = new List<ChunkMetadata>
            {
                TestDataHelper.CreateTestChunkMetadata(fileId, 0, 1024),
                TestDataHelper.CreateTestChunkMetadata(fileId, 1, 1024),
                TestDataHelper.CreateTestChunkMetadata(fileId, 2, 1024)
            };
            
            // Mock cursor
            var mockCursor = new Mock<IAsyncCursor<ChunkMetadata>>();
            mockCursor
                .Setup(c => c.Current)
                .Returns(chunks);
            mockCursor
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
                
            // Mock collection
            var mockCollection = new Mock<IMongoCollection<ChunkMetadata>>();
            mockCollection
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<ChunkMetadata>>(),
                    It.IsAny<FindOptions<ChunkMetadata, ChunkMetadata>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCursor.Object);
                
            // Create repository
            var repository = SetupRepositoryWithMockCollection(mockLogger.Object, mockCollection.Object);
            
            // Act
            var correlationId = Guid.NewGuid();
            var results = await repository.GetChunksByFileIdAsync(fileId, correlationId);
            
            // Assert
            Assert.NotNull(results);
            var resultsList = results.ToList();
            Assert.Equal(3, resultsList.Count);
            Assert.All(resultsList, c => Assert.Equal(fileId, c.FileId));
        }
        
        [Fact]
        public async Task UpdateAsync_UpdatesChunkMetadata()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<ChunkMetadataRepository>();
            
            // Create test data
            var chunk = TestDataHelper.CreateTestChunkMetadata("test-file-001", 1, 1024);
            chunk.IsCompressed = true;
            
            // Mock collection
            var mockCollection = new Mock<IMongoCollection<ChunkMetadata>>();
            var mockResult = new Mock<ReplaceOneResult>();
            mockResult.Setup(r => r.ModifiedCount).Returns(1);
            mockResult.Setup(r => r.IsAcknowledged).Returns(true);
            
            mockCollection
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<ChunkMetadata>>(),
                    It.IsAny<ChunkMetadata>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResult.Object);
                
            // Create repository
            var repository = SetupRepositoryWithMockCollection(mockLogger.Object, mockCollection.Object);
            
            // Act
            var correlationId = Guid.NewGuid();
            var result = await repository.UpdateAsync(chunk, correlationId);
            
            // Assert
            Assert.True(result);
            
            mockCollection.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<ChunkMetadata>>(),
                    It.Is<ChunkMetadata>(cm => cm.Id == chunk.Id && cm.IsCompressed),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task DeleteAsync_RemovesChunkMetadata()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<ChunkMetadataRepository>();
            
            // Create test data
            string chunkId = "test-chunk-to-delete";
            
            // Mock collection
            var mockCollection = new Mock<IMongoCollection<ChunkMetadata>>();
            var mockResult = new Mock<DeleteResult>();
            mockResult.Setup(r => r.DeletedCount).Returns(1);
            mockResult.Setup(r => r.IsAcknowledged).Returns(true);
            
            mockCollection
                .Setup(c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<ChunkMetadata>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResult.Object);
                
            // Create repository
            var repository = SetupRepositoryWithMockCollection(mockLogger.Object, mockCollection.Object);
            
            // Act
            var correlationId = Guid.NewGuid();
            var result = await repository.DeleteAsync(chunkId, correlationId);
            
            // Assert
            Assert.True(result);
            
            mockCollection.Verify(
                c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<ChunkMetadata>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task DeleteChunksByFileIdAsync_RemovesAllChunksForFile()
        {
            // Arrange
            var mockLogger = _mockHelper.CreateMockLogger<ChunkMetadataRepository>();
            
            // Create test data
            string fileId = "test-file-to-delete-chunks";
            
            // Mock collection
            var mockCollection = new Mock<IMongoCollection<ChunkMetadata>>();
            var mockResult = new Mock<DeleteResult>();
            mockResult.Setup(r => r.DeletedCount).Returns(3); // Assuming 3 chunks were deleted
            mockResult.Setup(r => r.IsAcknowledged).Returns(true);
            
            mockCollection
                .Setup(c => c.DeleteManyAsync(
                    It.IsAny<FilterDefinition<ChunkMetadata>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResult.Object);
                
            // Create repository
            var repository = SetupRepositoryWithMockCollection(mockLogger.Object, mockCollection.Object);
            
            // Act
            var correlationId = Guid.NewGuid();
            var result = await repository.DeleteChunksByFileIdAsync(fileId, correlationId);
            
            // Assert
            Assert.True(result);
            
            mockCollection.Verify(
                c => c.DeleteManyAsync(
                    It.IsAny<FilterDefinition<ChunkMetadata>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        // Helper method to set up repository with mocked collection
        private ChunkMetadataRepository SetupRepositoryWithMockCollection(
            ILogger<ChunkMetadataRepository> logger,
            IMongoCollection<ChunkMetadata> mockCollection)
        {
            // Mock database
            var mockDatabase = new Mock<IMongoDatabase>();
            mockDatabase
                .Setup(d => d.GetCollection<ChunkMetadata>(
                    It.IsAny<string>(),
                    It.IsAny<MongoCollectionSettings>()))
                .Returns(mockCollection);
                
            // Mock client
            var mockClient = new Mock<IMongoClient>();
            mockClient
                .Setup(c => c.GetDatabase(
                    It.IsAny<string>(),
                    It.IsAny<MongoDatabaseSettings>()))
                .Returns(mockDatabase.Object);
                
            // Mock configuration
            var configuration = _mockHelper.CreateMockConfiguration(new Dictionary<string, string>
            {
                {"ConnectionStrings:MongoDB", "mongodb://localhost:27017"},
                {"DatabaseSettings:DatabaseName", "ChunckerTestDb"},
                {"DatabaseSettings:ChunkCollectionName", "Chunks"}
            });
            
            // Create repository using reflection to bypass constructor
            var repository = new ChunkMetadataRepository(configuration, logger);
            
            // Use reflection to set the private _collection field
            var field = typeof(MongoRepositoryBase<ChunkMetadata>).GetField("_collection", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(repository, mockCollection);
                
            return repository;
        }
    }
}
