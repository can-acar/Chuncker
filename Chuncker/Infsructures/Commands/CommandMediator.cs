using Chuncker.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Chuncker.Infsructures.Commands
{
    /// <summary>
    /// Chain of Responsibility destekli command mediator uygulaması
    /// </summary>
    public class CommandMediator : ICommandMediator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommandMediator> _logger;

        public CommandMediator(IServiceProvider serviceProvider, ILogger<CommandMediator> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Komut middleware zinciri aracılığıyla işlenmek üzere gönderir
        /// </summary>
        public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
            where TCommand : ICommand
        {
            _logger.LogDebug("Processing command: {CommandType}, CorrelationId: {CorrelationId}", 
                typeof(TCommand).Name, command.CorrelationId);

            // Get all middleware for this command type and order them
            var middlewares = GetOrderedMiddlewares<ICommandMiddleware<TCommand>>();
            
            _logger.LogDebug("Middleware execution order for {CommandType}: {MiddlewareOrder}", 
                typeof(TCommand).Name, 
                string.Join(" -> ", middlewares.Select(m => $"{m.GetType().Name}({GetMiddlewareOrder(m)})")));
            
            // Get the command handler
            var handler = _serviceProvider.GetRequiredService<ICommandHandler<TCommand>>();

            // Build middleware chain
            var pipeline = BuildPipeline(middlewares, async () => 
            {
                await handler.HandleAsync(command, cancellationToken);
            }, command);

            // Execute pipeline
            await pipeline();

            _logger.LogDebug("Command processed successfully: {CommandType}, CorrelationId: {CorrelationId}", 
                typeof(TCommand).Name, command.CorrelationId);
        }

        /// <summary>
        /// Komut gönderir ve middleware zinciri aracılığıyla sonuç alır
        /// </summary>
        public async Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) 
            where TCommand : ICommand<TResult>
        {
            _logger.LogDebug("Processing command with result: {CommandType}, CorrelationId: {CorrelationId}", 
                typeof(TCommand).Name, command.CorrelationId);

            // Get all middleware for this command type and order them
            var middlewares = GetOrderedMiddlewares<ICommandMiddleware<TCommand, TResult>>();
            
            _logger.LogDebug("Middleware execution order for {CommandType}: {MiddlewareOrder}", 
                typeof(TCommand).Name, 
                string.Join(" -> ", middlewares.Select(m => $"{m.GetType().Name}({GetMiddlewareOrder(m)})")));
            
            // Get the command handler
            var handler = _serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>();

            // Build middleware chain
            var pipeline = BuildPipeline(middlewares, async () => 
            {
                return await handler.HandleAsync(command, cancellationToken);
            }, command);

            // Execute pipeline
            var result = await pipeline();

            _logger.LogDebug("Command processed successfully with result: {CommandType}, CorrelationId: {CorrelationId}", 
                typeof(TCommand).Name, command.CorrelationId);

            return result;
        }

        /// <summary>
        /// Middleware pipeline'ini oluşturur (Chain of Responsibility)
        /// </summary>
        private Func<Task> BuildPipeline<TCommand>(
            IList<ICommandMiddleware<TCommand>> middlewares, 
            Func<Task> finalHandler,
            TCommand command) where TCommand : ICommand
        {
            return middlewares.Reverse().Aggregate(
                finalHandler,
                (next, middleware) => () => middleware.HandleAsync(command, next));
        }

        /// <summary>
        /// Dönüş değeri olan middleware pipeline'ini oluşturur
        /// </summary>
        private Func<Task<TResult>> BuildPipeline<TCommand, TResult>(
            IList<ICommandMiddleware<TCommand, TResult>> middlewares, 
            Func<Task<TResult>> finalHandler,
            TCommand command) where TCommand : ICommand<TResult>
        {
            return middlewares.Reverse().Aggregate(
                finalHandler,
                (next, middleware) => () => middleware.HandleAsync(command, next));
        }

        /// <summary>
        /// Middleware'leri sıraya koyar ve döndürür
        /// </summary>
        /// <typeparam name="TMiddleware">Middleware interface türü</typeparam>
        /// <returns>Sıralı middleware listesi</returns>
        private IList<TMiddleware> GetOrderedMiddlewares<TMiddleware>()
        {
            var middlewares = _serviceProvider.GetServices<TMiddleware>().ToList();
            
            // Middleware'leri order değerine göre sırala
            return middlewares
                .Select(middleware => new
                {
                    Middleware = middleware,
                    Order = GetMiddlewareOrder(middleware)
                })
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Middleware?.GetType().Name) // Aynı order değeri varsa alfabetik sırala
                .Select(x => x.Middleware)
                .ToList();
        }

        /// <summary>
        /// Middleware'in order değerini alır
        /// </summary>
        /// <param name="middleware">Middleware instance</param>
        /// <returns>Order değeri</returns>
        private int GetMiddlewareOrder(object middleware)
        {
            if (middleware == null)
                return MiddlewareOrder.Default;

            // IOrderedMiddleware interface'ini kontrol et
            if (middleware is IOrderedMiddleware orderedMiddleware)
            {
                var order = orderedMiddleware.Order;
                _logger.LogDebug("Middleware {MiddlewareType} has explicit order: {Order}", 
                    middleware.GetType().Name, order);
                return order;
            }

            // MiddlewareOrder attribute'unu kontrol et
            var middlewareType = middleware.GetType();
            var orderAttribute = middlewareType.GetCustomAttribute<MiddlewareOrderAttribute>();
            if (orderAttribute != null)
            {
                _logger.LogDebug("Middleware {MiddlewareType} has attribute order: {Order}", 
                    middlewareType.Name, orderAttribute.Order);
                return orderAttribute.Order;
            }

            // Generic type ise base type'ını kontrol et
            if (middlewareType.IsGenericType)
            {
                var genericTypeDefinition = middlewareType.GetGenericTypeDefinition();
                orderAttribute = genericTypeDefinition.GetCustomAttribute<MiddlewareOrderAttribute>();
                if (orderAttribute != null)
                {
                    _logger.LogDebug("Middleware {MiddlewareType} has generic type attribute order: {Order}", 
                        middlewareType.Name, orderAttribute.Order);
                    return orderAttribute.Order;
                }
            }

            // Default order kullan
            _logger.LogDebug("Middleware {MiddlewareType} using default order: {Order}", 
                middlewareType.Name, MiddlewareOrder.Default);
            return MiddlewareOrder.Default;
        }
    }
}