using System.Diagnostics;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.Middleware
{
    /// <summary>
    /// Komut hattı için performans izleme middleware'i
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    [MiddlewareOrder(MiddlewareOrder.Performance)]
    public class PerformanceMiddleware<TCommand> : ICommandMiddleware<TCommand>, IOrderedMiddleware where TCommand : ICommand
    {
        private readonly ILogger<PerformanceMiddleware<TCommand>> _logger;

        public PerformanceMiddleware(ILogger<PerformanceMiddleware<TCommand>> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Middleware çalıştırma sırası
        /// </summary>
        public int Order => MiddlewareOrder.Performance;

        public async Task HandleAsync(TCommand command, Func<Task> next, CancellationToken cancellationToken = default)
        {
            var commandName = typeof(TCommand).Name;
            var stopwatch = Stopwatch.StartNew();

            _logger.LogDebug("Performance monitoring started for command: {CommandName}, CorrelationId: {CorrelationId}", 
                commandName, command.CorrelationId);

            try
            {
                await next();
                
                stopwatch.Stop();
                
                // Performans metrikleri logla
                if (stopwatch.ElapsedMilliseconds > 1000) // Yavaş komut eşiği
                {
                    _logger.LogWarning("Slow command detected: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                        commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
                }
                else
                {
                    _logger.LogDebug("Performance monitoring completed for command: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                        commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Performance monitoring - Command failed: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                    commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
                throw;
            }
        }
    }

    /// <summary>
    /// Dönüş değeri olan komut hattı için performans izleme middleware'i
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TResult">Return type</typeparam>
    [MiddlewareOrder(MiddlewareOrder.Performance)]
    public class PerformanceMiddleware<TCommand, TResult> : ICommandMiddleware<TCommand, TResult>, IOrderedMiddleware 
        where TCommand : ICommand<TResult>
    {
        private readonly ILogger<PerformanceMiddleware<TCommand, TResult>> _logger;

        public PerformanceMiddleware(ILogger<PerformanceMiddleware<TCommand, TResult>> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Middleware çalıştırma sırası
        /// </summary>
        public int Order => MiddlewareOrder.Performance;

        public async Task<TResult> HandleAsync(TCommand command, Func<Task<TResult>> next, CancellationToken cancellationToken = default)
        {
            var commandName = typeof(TCommand).Name;
            var stopwatch = Stopwatch.StartNew();

            _logger.LogDebug("Performance monitoring started for command: {CommandName}, CorrelationId: {CorrelationId}", 
                commandName, command.CorrelationId);

            try
            {
                var result = await next();
                
                stopwatch.Stop();
                
                // Performans metrikleri logla
                if (stopwatch.ElapsedMilliseconds > 1000) // Yavaş komut eşiği
                {
                    _logger.LogWarning("Slow command detected: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                        commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
                }
                else
                {
                    _logger.LogDebug("Performance monitoring completed for command: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                        commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Performance monitoring - Command failed: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                    commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
                throw;
            }
        }
    }
}