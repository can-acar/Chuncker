using System.Collections.Concurrent;
using System.Reflection;
using Chuncker.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chuncker.Infsructures.Events
{
    /// <summary>
    /// Event yayınlayıcı uygulaması
    /// </summary>
    public class EventPublisher : IEventPublisher
    {
        private readonly ILogger<EventPublisher> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<Type, List<Type>> _handlersMap;
        private readonly object _lockObject = new object();
        private bool _handlersDiscovered = false;

        public EventPublisher(
            ILogger<EventPublisher> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _handlersMap = new ConcurrentDictionary<Type, List<Type>>();
        }

        /// <summary>
        /// Bir event handler'ı event türüne kaydeder
        /// </summary>
        /// <typeparam name="TEvent">Event türü</typeparam>
        /// <typeparam name="THandler">Handler türü</typeparam>
        public void RegisterHandler<TEvent, THandler>()
            where TEvent : IEvent
            where THandler : IEventHandler<TEvent>
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            if (!_handlersMap.ContainsKey(eventType))
            {
                _handlersMap[eventType] = new List<Type>();
            }

            if (!_handlersMap[eventType].Contains(handlerType))
            {
                _handlersMap[eventType].Add(handlerType);
            }
        }

        /// <summary>
        /// Otomatik olarak tüm assembly'lerden event handler'ları keşfeder ve kaydeder
        /// </summary>
        private void DiscoverAndRegisterHandlers()
        {
            if (_handlersDiscovered)
                return;

            lock (_lockObject)
            {
                if (_handlersDiscovered)
                    return;

                _logger.LogInformation("Event handler'lar otomatik olarak keşfediliyor...");

                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && a.FullName != null && 
                               (a.FullName.StartsWith("Chuncker") || a.FullName.StartsWith("System") == false))
                    .ToArray();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var handlerTypes = assembly.GetTypes()
                            .Where(type => !type.IsAbstract && !type.IsInterface)
                            .Where(type => type.GetInterfaces()
                                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)))
                            .ToArray();

                        foreach (var handlerType in handlerTypes)
                        {
                            var eventHandlerInterfaces = handlerType.GetInterfaces()
                                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

                            foreach (var interfaceType in eventHandlerInterfaces)
                            {
                                var eventType = interfaceType.GetGenericArguments()[0];
                                
                                if (!_handlersMap.ContainsKey(eventType))
                                {
                                    _handlersMap[eventType] = new List<Type>();
                                }

                                if (!_handlersMap[eventType].Contains(handlerType))
                                {
                                    _handlersMap[eventType].Add(handlerType);
                                    _logger.LogDebug("Event handler kaydedildi: {HandlerType} -> {EventType}", 
                                        handlerType.Name, eventType.Name);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Assembly taranırken hata oluştu: {AssemblyName}", assembly.FullName);
                    }
                }

                _handlersDiscovered = true;
                _logger.LogInformation("Event handler keşfi tamamlandı. Toplam {EventTypeCount} event türü için {HandlerCount} handler kaydedildi.",
                    _handlersMap.Count, _handlersMap.Sum(kvp => kvp.Value.Count));
            }
        }

        /// <summary>
        /// Bir event'i yayınlar ve ilgili tüm handler'ları çağırır
        /// </summary>
        /// <typeparam name="TEvent">Event türü</typeparam>
        /// <param name="event">Yayınlanacak event</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            // İlk kullanımda handler'ları otomatik keşfet
            DiscoverAndRegisterHandlers();

            var eventType = @event.GetType();
            _logger.LogInformation("Event yayınlanıyor: {EventType}, CorrelationId: {CorrelationId}", eventType.Name, @event.CorrelationId);

            if (!_handlersMap.TryGetValue(eventType, out var handlerTypes) || handlerTypes.Count == 0)
            {
                _logger.LogWarning("Event için handler bulunamadı: {EventType}", eventType.Name);
                return;
            }

            var tasks = new List<Task>();

            foreach (var handlerType in handlerTypes)
            {
                try
                {
                    // Handler örneğini oluştur
                    var handler = _serviceProvider.GetService(handlerType);

                    if (handler == null)
                    {
                        _logger.LogError("Handler örneği oluşturulamadı: {HandlerType}", handlerType.Name);
                        continue;
                    }

                    // Handler'ın HandleAsync metodunu çağır
                    var method = handlerType.GetMethod("HandleAsync");
                    if (method != null)
                    {
                        var task = (Task)method.Invoke(handler, new object[] { @event, cancellationToken });
                        tasks.Add(task);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Event handler çalıştırılırken hata oluştu: {HandlerType}, {EventType}", handlerType.Name, eventType.Name);
                }
            }

            await Task.WhenAll(tasks);
        }
    }
}
