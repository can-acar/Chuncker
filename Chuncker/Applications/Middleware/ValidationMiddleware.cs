using System.ComponentModel.DataAnnotations;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Applications.Middleware
{
    /// <summary>
    /// Komut hattı için doğrulama middleware'i
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    [MiddlewareOrder(MiddlewareOrder.Validation)]
    public class ValidationMiddleware<TCommand> : ICommandMiddleware<TCommand>, IOrderedMiddleware where TCommand : ICommand
    {
        private readonly ILogger<ValidationMiddleware<TCommand>> _logger;

        public ValidationMiddleware(ILogger<ValidationMiddleware<TCommand>> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Middleware çalıştırma sırası
        /// </summary>
        public int Order => MiddlewareOrder.Validation;

        public async Task HandleAsync(TCommand command, Func<Task> next, CancellationToken cancellationToken = default)
        {
            await ValidateCommand(command);
            await next();
        }

        private async Task ValidateCommand(TCommand command)
        {
            var validationContext = new ValidationContext(command);
            var validationResults = new System.Collections.Generic.List<ValidationResult>();

            if (!Validator.TryValidateObject(command, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(vr => vr.ErrorMessage).ToArray();
                var errorMessage = $"Command validation failed: {string.Join(", ", errors)}";
                
                _logger.LogWarning("Command validation failed: {CommandName}, Errors: {Errors}, CorrelationId: {CorrelationId}", 
                    typeof(TCommand).Name, string.Join(", ", errors), command.CorrelationId);

                throw new ValidationException(errorMessage);
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Dönüş değeri olan komut hattı için doğrulama middleware'i
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TResult">Return type</typeparam>
    [MiddlewareOrder(MiddlewareOrder.Validation)]
    public class ValidationMiddleware<TCommand, TResult> : ICommandMiddleware<TCommand, TResult>, IOrderedMiddleware 
        where TCommand : ICommand<TResult>
    {
        private readonly ILogger<ValidationMiddleware<TCommand, TResult>> _logger;

        public ValidationMiddleware(ILogger<ValidationMiddleware<TCommand, TResult>> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Middleware çalıştırma sırası
        /// </summary>
        public int Order => MiddlewareOrder.Validation;

        public async Task<TResult> HandleAsync(TCommand command, Func<Task<TResult>> next, CancellationToken cancellationToken = default)
        {
            await ValidateCommand(command);
            return await next();
        }

        private async Task ValidateCommand(TCommand command)
        {
            var validationContext = new ValidationContext(command);
            var validationResults = new System.Collections.Generic.List<ValidationResult>();

            if (!Validator.TryValidateObject(command, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(vr => vr.ErrorMessage).ToArray();
                var errorMessage = $"Command validation failed: {string.Join(", ", errors)}";
                
                _logger.LogWarning("Command validation failed: {CommandName}, Errors: {Errors}, CorrelationId: {CorrelationId}", 
                    typeof(TCommand).Name, string.Join(", ", errors), command.CorrelationId);

                throw new ValidationException(errorMessage);
            }

            await Task.CompletedTask;
        }
    }
}