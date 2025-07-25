using Chuncker.Interfaces;
using Chuncker.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Chuncker.Infsructures.Factories
{
    /// <summary>
    /// Storage provider oluşturmak için factory sınıfı
    /// </summary>
    public class StorageProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StorageProviderFactory> _logger;
        private readonly Dictionary<string, Type> _providerTypes;

        /// <summary>
        /// Yeni bir StorageProviderFactory örneği oluşturur
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="logger">Logger</param>
        public StorageProviderFactory(
            IServiceProvider serviceProvider,
            ILogger<StorageProviderFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _providerTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            // Bilinen provider türlerini kaydet
            RegisterProviderType("filesystem", typeof(FileSystemStorageProvider));
            RegisterProviderType("mongodb", typeof(MongoDbStorageProvider));
        }

        /// <summary>
        /// Yeni bir provider türünü kaydeder
        /// </summary>
        /// <param name="providerType">Provider türü adı</param>
        /// <param name="implementationType">Uygulama sınıfı türü</param>
        public void RegisterProviderType(string providerType, Type implementationType)
        {
            if (string.IsNullOrWhiteSpace(providerType))
            {
                throw new ArgumentNullException(nameof(providerType));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            if (!typeof(IStorageProvider).IsAssignableFrom(implementationType))
            {
                throw new ArgumentException($"{implementationType.Name} türü IStorageProvider arayüzünü uygulamıyor.");
            }

            _providerTypes[providerType] = implementationType;
            _logger.LogInformation("Provider türü kaydedildi: {ProviderType} -> {ImplementationType}", 
                providerType, implementationType.Name);
        }

        /// <summary>
        /// Belirtilen türde bir storage provider oluşturur
        /// </summary>
        /// <param name="providerType">Provider türü</param>
        /// <returns>Oluşturulan storage provider</returns>
        public IStorageProvider CreateProvider(string providerType)
        {
            if (string.IsNullOrWhiteSpace(providerType))
            {
                throw new ArgumentNullException(nameof(providerType));
            }

            if (!_providerTypes.TryGetValue(providerType, out var implementationType))
            {
                _logger.LogError("Desteklenmeyen provider türü: {ProviderType}", providerType);
                throw new ArgumentException($"Desteklenmeyen provider türü: {providerType}");
            }

            try
            {
                var provider = (IStorageProvider)ActivatorUtilities.CreateInstance(_serviceProvider, implementationType);
                _logger.LogInformation("Provider başarıyla oluşturuldu: {ProviderType} -> {ImplementationType}", 
                    providerType, implementationType.Name);
                return provider;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provider oluşturulurken hata oluştu: {ProviderType}", providerType);
                throw;
            }
        }

        /// <summary>
        /// Yapılandırmada tanımlanan tüm storage provider'ları oluşturur
        /// </summary>
        /// <param name="configuration">Uygulama yapılandırması</param>
        /// <returns>Oluşturulan storage provider'ların listesi</returns>
        public IEnumerable<IStorageProvider> CreateProvidersFromConfiguration(IConfiguration configuration)
        {
            var providers = new List<IStorageProvider>();
            var providerTypes = configuration.GetSection("StorageProviders:EnabledProviders").Get<string[]>();

            if (providerTypes == null || !providerTypes.Any())
            {
                _logger.LogInformation("Yapılandırmada tanımlı provider bulunamadı, varsayılan provider'lar kullanılıyor.");
                providerTypes = new[] { "filesystem", "mongodb" };
            }

            foreach (var providerType in providerTypes)
            {
                try
                {
                    providers.Add(CreateProvider(providerType));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Provider oluşturulurken hata oluştu ve atlandı: {ProviderType}", providerType);
                }
            }

            _logger.LogInformation("Toplam {Count} provider oluşturuldu: {Providers}", 
                providers.Count, string.Join(", ", providers.Select(p => p.ProviderType)));

            return providers;
        }
    }
}
