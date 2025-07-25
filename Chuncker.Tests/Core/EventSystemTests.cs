using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Interfaces;
using Chuncker.Infsructures.Events;
using Chuncker.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Chuncker.Tests.Core
{
    public class EventSystemTests
    {
        private readonly TestMockHelper _mockHelper;
        private readonly ITestOutputHelper _output;
        
        public EventSystemTests(ITestOutputHelper output)
        {
            _output = output;
            _mockHelper = new TestMockHelper(output);
        }
        
        public class TestEvent : IEvent
        {
            public Guid EventId { get; } = Guid.NewGuid();
            public string EventType => "TestEvent";
            public DateTime Timestamp { get; } = DateTime.UtcNow;
            public Guid CorrelationId { get; }
            public string Data { get; }
            
            public TestEvent(string data, Guid correlationId)
            {
                Data = data;
                CorrelationId = correlationId;
            }
        }
        
        public class TestEventHandler : IEventHandler<TestEvent>
        {
            public bool WasHandled { get; private set; }
            public TestEvent LastEvent { get; private set; }
            
            public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
            {
                WasHandled = true;
                LastEvent = @event;
                return Task.CompletedTask;
            }
        }
        
        public class SecondTestEventHandler : IEventHandler<TestEvent>
        {
            public bool WasHandled { get; private set; }
            public TestEvent LastEvent { get; private set; }
            
            public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
            {
                WasHandled = true;
                LastEvent = @event;
                return Task.CompletedTask;
            }
        }
        
        public class ExceptionThrowingHandler : IEventHandler<TestEvent>
        {
            public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Test exception");
            }
        }
        
        [Fact]
        public async Task EventPublisher_DeliversEventToHandler()
        {
            // Arrange
            var logger = _mockHelper.CreateMockLogger<EventPublisher>();
            var handler = new TestEventHandler();
            
            // Build a service provider that returns our handler when requested by type
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(TestEventHandler)))
                .Returns(handler);
                
            // Create the event publisher with our mocked service provider
            var eventPublisher = new EventPublisher(logger.Object, mockServiceProvider.Object);
            
            // Register our handler for the event type
            eventPublisher.RegisterHandler<TestEvent, TestEventHandler>();
            
            // Create the test event
            var correlationId = Guid.NewGuid();
            var testEvent = new TestEvent("Test data", correlationId);
            
            // Act
            await eventPublisher.PublishAsync(testEvent);
            
            // Assert
            Assert.True(handler.WasHandled);
            Assert.Equal(testEvent.EventId, handler.LastEvent.EventId);
            Assert.Equal(testEvent.Data, handler.LastEvent.Data);
            Assert.Equal(correlationId, handler.LastEvent.CorrelationId);
        }
        
        [Fact]
        public async Task EventPublisher_HandlesMultipleHandlersForEvent()
        {
            // Arrange
            var logger = _mockHelper.CreateMockLogger<EventPublisher>();
            var handler1 = new TestEventHandler();
            var handler2 = new SecondTestEventHandler();
            
            // Build a service provider that returns different handlers based on requested type
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(TestEventHandler)))
                .Returns(handler1);
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(SecondTestEventHandler)))
                .Returns(handler2);
            
            // Create the event publisher
            var eventPublisher = new EventPublisher(logger.Object, mockServiceProvider.Object);
            
            // Register both handlers for the event type
            eventPublisher.RegisterHandler<TestEvent, TestEventHandler>();
            eventPublisher.RegisterHandler<TestEvent, SecondTestEventHandler>();
            
            // Create the test event
            var correlationId = Guid.NewGuid();
            var testEvent = new TestEvent("Multiple handlers test", correlationId);
            
            // Act
            await eventPublisher.PublishAsync(testEvent);
            
            // Assert
            Assert.True(handler1.WasHandled);
            Assert.True(handler2.WasHandled);
        }
        
        [Fact]
        public async Task EventPublisher_HandlesExceptionInHandler()
        {
            // Arrange
            var logger = _mockHelper.CreateMockLogger<EventPublisher>();
            
            // Create an exception-throwing handler
            var exceptionHandler = new ExceptionThrowingHandler();
                
            // Build a service provider that returns our handler
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(ExceptionThrowingHandler)))
                .Returns(exceptionHandler);
            
            // Create the event publisher
            var eventPublisher = new EventPublisher(logger.Object, mockServiceProvider.Object);
            
            // Register our handler for the event type
            eventPublisher.RegisterHandler<TestEvent, ExceptionThrowingHandler>();
            
            // Create the test event
            var correlationId = Guid.NewGuid();
            var testEvent = new TestEvent("Exception test", correlationId);
            
            // Act - Should not throw exception
            await eventPublisher.PublishAsync(testEvent);
            
            // Assert - Event was published and should have been handled but with exception caught
            // No assertion needed here as the test passes if PublishAsync doesn't throw
                
            // Error should have been logged
            logger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }
        
        [Fact]
        public async Task EventPublisher_EventCorrelationIdTest()
        {
            // Arrange
            var logger = _mockHelper.CreateMockLogger<EventPublisher>();
            var handler = new TestEventHandler();
            
            // Build a service provider that returns our handler
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(TestEventHandler)))
                .Returns(handler);
            
            // Create the event publisher
            var eventPublisher = new EventPublisher(logger.Object, mockServiceProvider.Object);
            
            // Register our handler for the event type
            eventPublisher.RegisterHandler<TestEvent, TestEventHandler>();
            
            // Create the test event with a specific correlation ID
            var correlationId = Guid.NewGuid();
            var testEvent = new TestEvent("Correlation test", correlationId);
            
            // Act
            await eventPublisher.PublishAsync(testEvent);
            
            // Assert
            Assert.True(handler.WasHandled);
            Assert.Equal(correlationId, handler.LastEvent.CorrelationId);
            
            // Verify logging contains correlation ID
            logger.Verify(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(correlationId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }
    }
}
