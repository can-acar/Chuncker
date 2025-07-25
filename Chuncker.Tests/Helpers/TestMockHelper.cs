using System;
using System.Collections.Generic;
using Chuncker.Interfaces;
using Chuncker.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace Chuncker.Tests.Helpers
{
    /// <summary>
    /// Test sınıfları için temel mocking işlemlerini içeren yardımcı sınıf
    /// </summary>
    public class TestMockHelper
    {
        private readonly ITestOutputHelper _testOutputHelper;
        
        public TestMockHelper(ITestOutputHelper testOutputHelper = null)
        {
            _testOutputHelper = testOutputHelper;
        }
        
        /// <summary>
        /// Mock bir ILogger oluşturur
        /// </summary>
        /// <typeparam name="T">Logger sınıfı tipi</typeparam>
        /// <returns>Mock ILogger</returns>
        public Mock<ILogger<T>> CreateMockLogger<T>()
        {
            var mockLogger = new Mock<ILogger<T>>();
            
            // Simple setup without callback to avoid Moq type inference issues
            mockLogger
                .Setup(x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<object, Exception, string>>()))
                .Verifiable();
            
            // Setup IsEnabled to return true for any log level
            mockLogger
                .Setup(x => x.IsEnabled(It.IsAny<LogLevel>()))
                .Returns(true);
            
            return mockLogger;
        }
        
        /// <summary>
        /// Mock bir IConfiguration nesnesi oluşturur
        /// </summary>
        /// <param name="settings">Anahtar-değer çiftleri şeklinde ayarlar</param>
        /// <returns>Mock IConfiguration</returns>
        public IConfiguration CreateMockConfiguration(Dictionary<string, string> settings = null)
        {
            settings ??= new Dictionary<string, string>
            {
                {"ChunkSettings:MinChunkSize", "32768"},  // 32 KB
                {"ChunkSettings:MaxChunkSize", "4194304"}, // 4 MB
                {"ChunkSettings:DefaultChunkSize", "1048576"}, // 1 MB
                {"ChunkSettings:CompressionEnabled", "true"},
                {"ChunkSettings:MaxConcurrentOperations", "4"},
                {"StorageProviderSettings:FileSystemPath", "./TestStorage/Files"}
            };
            
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }
        
        /// <summary>
        /// Mock bir ILoggerFactory oluşturur
        /// </summary>
        /// <returns>Mock ILoggerFactory</returns>
        public Mock<ILoggerFactory> CreateMockLoggerFactory()
        {
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory
                .Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns((string categoryName) => Mock.Of<ILogger>());
            return mockLoggerFactory;
        }
        
        /// <summary>
        /// Mock bir IEventPublisher oluşturur
        /// </summary>
        /// <returns>Mock IEventPublisher</returns>
        public Mock<IEventPublisher> CreateMockEventPublisher()
        {
            var mockPublisher = new Mock<IEventPublisher>();
            return mockPublisher;
        }
        
        /// <summary>
        /// Mock bir IChunkMetadataRepository oluşturur
        /// </summary>
        /// <returns>Mock IChunkMetadataRepository</returns>
        public Mock<IChunkMetadataRepository> CreateMockChunkRepository()
        {
            var mockRepository = new Mock<IChunkMetadataRepository>();
            return mockRepository;
        }
        
        /// <summary>
        /// Mock bir IFileMetadataRepository oluşturur
        /// </summary>
        /// <returns>Mock IFileMetadataRepository</returns>
        public Mock<IFileMetadataRepository> CreateMockFileRepository()
        {
            var mockRepository = new Mock<IFileMetadataRepository>();
            return mockRepository;
        }
        
        /// <summary>
        /// Mock bir IStorageProvider oluşturur
        /// </summary>
        /// <param name="providerId">Provider ID</param>
        /// <param name="providerType">Provider type</param>
        /// <returns>Mock IStorageProvider</returns>
        public Mock<IStorageProvider> CreateMockStorageProvider(string providerId = "test-provider", string providerType = "test")
        {
            var mockProvider = new Mock<IStorageProvider>();
            mockProvider.Setup(p => p.ProviderId).Returns(providerId);
            mockProvider.Setup(p => p.ProviderType).Returns(providerType);
            return mockProvider;
        }
    }
}
