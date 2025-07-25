using Chuncker.Interfaces;
using System.IO;

namespace Chuncker.Infsructures.Cli
{
    /// <summary>
    /// CLI komutunun temel uygulaması
    /// </summary>
    public abstract class CliCommandBase : ICliCommand
    {
        private readonly List<ICliArgument> _arguments = new();
        private readonly List<ICliOption> _options = new();

        /// <summary>
        /// Komutun adını alır
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Komutun açıklamasını alır
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Komut için argüman tanımlarını alır
        /// </summary>
        public IEnumerable<ICliArgument> Arguments => _arguments;

        /// <summary>
        /// Komut için seçenek tanımlarını alır
        /// </summary>
        public IEnumerable<ICliOption> Options => _options;

        /// <summary>
        /// <see cref="CliCommandBase"/> sınıfının yeni bir örneğini başlatır
        /// </summary>
        /// <param name="name">Komutun adı</param>
        /// <param name="description">Komutun açıklaması</param>
        protected CliCommandBase(string name, string description)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        /// <summary>
        /// Komuta bir argüman ekler
        /// </summary>
        /// <param name="argument">Eklenecek argüman</param>
        protected void AddArgument(ICliArgument argument)
        {
            _arguments.Add(argument ?? throw new ArgumentNullException(nameof(argument)));
        }

        /// <summary>
        /// Komuta bir seçenek ekler
        /// </summary>
        /// <param name="option">Eklenecek seçenek</param>
        protected void AddOption(ICliOption option)
        {
            _options.Add(option ?? throw new ArgumentNullException(nameof(option)));
        }

        /// <summary>
        /// Komutu sağlanan argümanlar ve seçeneklerle çalıştırır
        /// </summary>
        /// <param name="context">Komut yürütme bağlamı</param>
        /// <param name="cancellationToken">İptal belirteci</param>
        /// <returns>Komut yürütmesini temsil eden görev</returns>
        public abstract Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// CLI argümanının uygulaması
    /// </summary>
    /// <typeparam name="T">Argüman değerinin türü</typeparam>
    public class CliArgument<T> : ICliArgument
    {
        /// <summary>
        /// Argümanın adını alır
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Argümanın açıklamasını alır
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Argümanın gerekli olup olmadığını alır
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// Argüman değerinin beklenen türünü alır
        /// </summary>
        public Type ValueType => typeof(T);

        /// <summary>
        /// <see cref="CliArgument{T}"/> sınıfının yeni bir örneğini başlatır
        /// </summary>
        /// <param name="name">Argümanın adı</param>
        /// <param name="description">Argümanın açıklaması</param>
        /// <param name="isRequired">Argümanın gerekli olup olmadığı</param>
        public CliArgument(string name, string description, bool isRequired = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            IsRequired = isRequired;
        }

        /// <summary>
        /// Sağlanan değeri doğrular ve beklenen türe dönüştürür
        /// </summary>
        /// <param name="value">Dönüştürülecek string değer</param>
        /// <param name="result">Dönüştürülen değer</param>
        /// <returns>Dönüştürme başarılıysa true, aksi halde false</returns>
        public bool TryConvertValue(string value, out object result)
        {
            try
            {
                if (typeof(T) == typeof(string))
                {
                    result = value;
                    return true;
                }

                if (typeof(T) == typeof(int) && int.TryParse(value, out var intValue))
                {
                    result = intValue;
                    return true;
                }

                if (typeof(T) == typeof(bool) && bool.TryParse(value, out var boolValue))
                {
                    result = boolValue;
                    return true;
                }

                if (typeof(T) == typeof(Guid) && Guid.TryParse(value, out var guidValue))
                {
                    result = guidValue;
                    return true;
                }

                if (typeof(T) == typeof(DirectoryInfo))
                {
                    try
                    {
                        result = new DirectoryInfo(value);
                        return true;
                    }
                    catch
                    {
                        result = null;
                        return false;
                    }
                }

                if (typeof(T) == typeof(FileInfo))
                {
                    try
                    {
                        result = new FileInfo(value);
                        return true;
                    }
                    catch
                    {
                        result = null;
                        return false;
                    }
                }

                // Add more type conversions as needed

                result = null;
                return false;
            }
            catch
            {
                result = null;
                return false;
            }
        }
    }

    /// <summary>
    /// CLI seçeneğinin uygulaması
    /// </summary>
    /// <typeparam name="T">Seçenek değerinin türü</typeparam>
    public class CliOption<T> : ICliOption
    {
        /// <summary>
        /// Seçeneğin adını alır
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Seçeneğin kısa adını/takma adını alır
        /// </summary>
        public string ShortName { get; }

        /// <summary>
        /// Seçeneğin açıklamasını alır
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Seçenek değerinin beklenen türünü alır
        /// </summary>
        public Type ValueType => typeof(T);

        /// <summary>
        /// Seçeneğin varsayılan değerini alır
        /// </summary>
        public object DefaultValue { get; }

        /// <summary>
        /// <see cref="CliOption{T}"/> sınıfının yeni bir örneğini başlatır
        /// </summary>
        /// <param name="name">Seçeneğin adı</param>
        /// <param name="shortName">Seçeneğin kısa adı/takma adı</param>
        /// <param name="description">Seçeneğin açıklaması</param>
        /// <param name="defaultValue">Seçeneğin varsayılan değeri</param>
        public CliOption(string name, string shortName, string description, T defaultValue = default)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ShortName = shortName;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// Sağlanan değeri doğrular ve beklenen türe dönüştürür
        /// </summary>
        /// <param name="value">Dönüştürülecek string değer</param>
        /// <param name="result">Dönüştürülen değer</param>
        /// <returns>Dönüştürme başarılıysa true, aksi halde false</returns>
        public bool TryConvertValue(string value, out object result)
        {
            try
            {
                if (typeof(T) == typeof(string))
                {
                    result = value;
                    return true;
                }

                if (typeof(T) == typeof(int) && int.TryParse(value, out var intValue))
                {
                    result = intValue;
                    return true;
                }

                if (typeof(T) == typeof(bool))
                {
                    // Handle flags with no value
                    if (string.IsNullOrEmpty(value))
                    {
                        result = true;
                        return true;
                    }

                    if (bool.TryParse(value, out var boolValue))
                    {
                        result = boolValue;
                        return true;
                    }
                }

                if (typeof(T) == typeof(Guid) && Guid.TryParse(value, out var guidValue))
                {
                    result = guidValue;
                    return true;
                }

                if (typeof(T) == typeof(DirectoryInfo))
                {
                    try
                    {
                        result = new DirectoryInfo(value);
                        return true;
                    }
                    catch
                    {
                        result = null;
                        return false;
                    }
                }

                if (typeof(T) == typeof(FileInfo))
                {
                    try
                    {
                        result = new FileInfo(value);
                        return true;
                    }
                    catch
                    {
                        result = null;
                        return false;
                    }
                }

                // Add more type conversions as needed

                result = null;
                return false;
            }
            catch
            {
                result = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Bir komut için yürütme bağlamının uygulaması
    /// </summary>
    public class CliContext : ICliContext
    {
        private readonly Dictionary<string, object> _argumentValues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> _optionValues = new(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Servis sağlayıcıyı alır
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// <see cref="CliContext"/> sınıfının yeni bir örneğini başlatır
        /// </summary>
        /// <param name="serviceProvider">Servis sağlayıcı</param>
        public CliContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Argüman değerini ayarlar
        /// </summary>
        /// <param name="name">Argümanın adı</param>
        /// <param name="value">Argüman değeri</param>
        public void SetArgumentValue(string name, object value)
        {
            _argumentValues[name] = value;
        }

        /// <summary>
        /// Seçenek değerini ayarlar
        /// </summary>
        /// <param name="name">Seçeneğin adı</param>
        /// <param name="value">Seçenek değeri</param>
        public void SetOptionValue(string name, object value)
        {
            _optionValues[name] = value;
        }

        /// <summary>
        /// Argüman değerini alır
        /// </summary>
        /// <typeparam name="T">Argümanın beklenen türü</typeparam>
        /// <param name="name">Argümanın adı</param>
        /// <returns>Argüman değeri</returns>
        public T GetArgumentValue<T>(string name)
        {
            if (_argumentValues.TryGetValue(name, out var value))
            {
                return (T)value;
            }

            return default;
        }

        /// <summary>
        /// Seçenek değerini alır
        /// </summary>
        /// <typeparam name="T">Seçeneğin beklenen türü</typeparam>
        /// <param name="name">Seçeneğin adı</param>
        /// <returns>Seçenek değeri</returns>
        public T GetOptionValue<T>(string name)
        {
            if (_optionValues.TryGetValue(name, out var value))
            {
                return (T)value;
            }

            return default;
        }
    }
}
