using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chuncker.Interfaces
{
    /// <summary>
    /// Bir komut satırı arayüz komutu temsil eder
    /// </summary>
    public interface ICliCommand
    {
        /// <summary>
        /// Komutun adını alır
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Komutun açıklamasını alır
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Komut için argüman tanımlarını alır
        /// </summary>
        IEnumerable<ICliArgument> Arguments { get; }
        
        /// <summary>
        /// Komut için seçenek tanımlarını alır
        /// </summary>
        IEnumerable<ICliOption> Options { get; }
        
        /// <summary>
        /// Sağlanan argümanlar ve seçeneklerle komutu yürütür
        /// </summary>
        /// <param name="context">Komut yürütme bağlamı</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Komut yürütmesini temsil eden görev</returns>
        Task<int> ExecuteAsync(ICliContext context, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Bir komut satırı arayüz argümanı temsil eder
    /// </summary>
    public interface ICliArgument
    {
        /// <summary>
        /// Gets the name of the argument
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Gets the description of the argument
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Gets whether the argument is required
        /// </summary>
        bool IsRequired { get; }
        
        /// <summary>
        /// Gets the expected type of the argument value
        /// </summary>
        Type ValueType { get; }
        
        /// <summary>
        /// Validates and converts the provided value to the expected type
        /// </summary>
        /// <param name="value">The string value to convert</param>
        /// <param name="result">The converted value</param>
        /// <returns>True if conversion succeeded, false otherwise</returns>
        bool TryConvertValue(string value, out object result);
    }

    /// <summary>
    /// Represents a command-line interface option
    /// </summary>
    public interface ICliOption
    {
        /// <summary>
        /// Gets the name of the option
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Gets the short name/alias of the option
        /// </summary>
        string ShortName { get; }
        
        /// <summary>
        /// Gets the description of the option
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Gets the expected type of the option value
        /// </summary>
        Type ValueType { get; }
        
        /// <summary>
        /// Gets the default value for the option
        /// </summary>
        object DefaultValue { get; }
        
        /// <summary>
        /// Validates and converts the provided value to the expected type
        /// </summary>
        /// <param name="value">The string value to convert</param>
        /// <param name="result">The converted value</param>
        /// <returns>True if conversion succeeded, false otherwise</returns>
        bool TryConvertValue(string value, out object result);
    }

    /// <summary>
    /// Represents the execution context for a command
    /// </summary>
    public interface ICliContext
    {
        /// <summary>
        /// Gets the service provider
        /// </summary>
        IServiceProvider ServiceProvider { get; }
        
        /// <summary>
        /// Gets the argument value
        /// </summary>
        /// <typeparam name="T">The expected type of the argument</typeparam>
        /// <param name="name">The name of the argument</param>
        /// <returns>The argument value</returns>
        T GetArgumentValue<T>(string name);
        
        /// <summary>
        /// Gets the option value
        /// </summary>
        /// <typeparam name="T">The expected type of the option</typeparam>
        /// <param name="name">The name of the option</param>
        /// <returns>The option value</returns>
        T GetOptionValue<T>(string name);
    }
}
