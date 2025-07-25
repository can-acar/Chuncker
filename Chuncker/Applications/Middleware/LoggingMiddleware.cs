using System.Diagnostics;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.Middleware
{
    /// <summary>
    /// Komut hattı için loglama middleware'i
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    [MiddlewareOrder(MiddlewareOrder.Logging)]
    public class LoggingMiddleware<TCommand> : ICommandMiddleware<TCommand>, IOrderedMiddleware where TCommand : ICommand
    {
        private readonly ILogger<LoggingMiddleware<TCommand>> _logger;

        public LoggingMiddleware(ILogger<LoggingMiddleware<TCommand>> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Middleware çalıştırma sırası
        /// </summary>
        public int Order => MiddlewareOrder.Logging;

        public async Task HandleAsync(TCommand command, Func<Task> next, CancellationToken cancellationToken = default)
        {
            var commandName = typeof(TCommand).Name;
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Executing command: {CommandName}, CorrelationId: {CorrelationId}", 
                commandName, command.CorrelationId);

            try
            {
                await next();
                
                stopwatch.Stop();
                _logger.LogInformation("Command executed successfully: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                    commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Command execution failed: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                    commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
                throw;
            }
        }
    }

    /// <summary>
    /// Dönüş değeri olan komut hattı için loglama middleware'i
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TResult">Return type</typeparam>
    [MiddlewareOrder(MiddlewareOrder.Logging)]
    public class LoggingMiddleware<TCommand, TResult> : ICommandMiddleware<TCommand, TResult>, IOrderedMiddleware 
        where TCommand : ICommand<TResult>
    {
        private readonly ILogger<LoggingMiddleware<TCommand, TResult>> _logger;

        public LoggingMiddleware(ILogger<LoggingMiddleware<TCommand, TResult>> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Middleware çalıştırma sırası
        /// </summary>
        public int Order => MiddlewareOrder.Logging;

        public async Task<TResult> HandleAsync(TCommand command, Func<Task<TResult>> next, CancellationToken cancellationToken = default)
        {
            var commandName = typeof(TCommand).Name;
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Executing command: {CommandName}, CorrelationId: {CorrelationId}", 
                commandName, command.CorrelationId);

            try
            {
                var result = await next();
                
                stopwatch.Stop();
                _logger.LogInformation("Command executed successfully: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                    commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Command execution failed: {CommandName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", 
                    commandName, stopwatch.ElapsedMilliseconds, command.CorrelationId);
                throw;
            }
        }
    }
}