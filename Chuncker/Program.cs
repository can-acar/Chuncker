using Chuncker.Models;
using Chuncker.Repositories;
using Chuncker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Serilog;
using System.Text;
using Chuncker.Applications.Cli;
using Chuncker.Applications.CommandHandlers;
using Chuncker.Applications.Commands;
using Chuncker.Applications.EventHandlers;
using Chuncker.Applications.Events;
using Chuncker.Applications.Middleware;
using Chuncker.Infsructures.Cli;
using Chuncker.Infsructures.Commands;
using Chuncker.Infsructures.Events;
using Chuncker.Infsructures.Factories;
using Chuncker.Infsructures.Logging;
using Chuncker.Infsructures.Monitoring;
using Chuncker.Infsructures.UI;
using Chuncker.Interfaces;
using Chuncker.Providers;
using Amazon.S3;
using Serilog.Ui.Core.Extensions;
using Serilog.Ui.MongoDbProvider.Extensions;
using Serilog.Ui.Web.Extensions;

namespace Chuncker;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // MongoDB GUID serialization yapılandırması
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // Yapılandırma dosyasını yükle
        var configBuilder = new ConfigurationBuilder();

        // Önce mevcut dizinde Config/appsettings.json olup olmadığını kontrol et
        var currentDirConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "Config", "appsettings.json");

        // Eğer mevcut dizinde yoksa, uygulamanın bulunduğu dizine bak
        var appDirConfigPath = Path.Combine(AppContext.BaseDirectory, "Config", "appsettings.json");

        string configPath;
        if (File.Exists(currentDirConfigPath))
        {
            configPath = currentDirConfigPath;
            configBuilder.SetBasePath(Directory.GetCurrentDirectory());
        }
        else if (File.Exists(appDirConfigPath))
        {
            configPath = appDirConfigPath;
            configBuilder.SetBasePath(AppContext.BaseDirectory);
        }
        else
        {
            throw new FileNotFoundException(
                $"Configuration file not found. Looked in:\n- {currentDirConfigPath}\n- {appDirConfigPath}");
        }

        IConfiguration configuration = configBuilder
            .AddJsonFile(configPath, optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        try
        {
            // Loglama altyapısını yapılandır
            var services = new ServiceCollection();
            services.ConfigureLogging(configuration);

            // MongoDB log ayarlarını yapılandır
            LoggingContext.ConfigureMongoDbLogging(configuration);

            Log.Information("Chuncker uygulaması başlatılıyor...");

            // MongoDB için log koleksiyonu indekslerini oluştur
            var connectionString = configuration.GetConnectionString("MongoDB");
            var databaseName = configuration.GetSection("DatabaseSettings:DatabaseName").Value ?? "ChunckerDB";
            var logCollectionName = configuration.GetSection("DatabaseSettings:LogCollectionName").Value ?? "Logs";


            LoggingConfiguration.CreateLogIndexes(connectionString, databaseName, logCollectionName);

            // Dependency Injection Container'ı oluştur
            var serviceProvider = ConfigureServices(services, configuration);

            // Uygulama başlangıç mesajı
            Console.WriteLine("Chuncker - Dağıtık Dosya Depolama Sistemi");
            Console.WriteLine("=========================================");

            // CLI uygulamasını yapılandır
            var cliApp = ConfigureCliApplication(serviceProvider);

            // Eğer argüman verilmişse, komutları çalıştır
            if (args.Length > 0)
            {
                return await cliApp.ExecuteAsync(args);
            }

            // Argüman verilmemişse, interaktif mod başlat
            return await cliApp.RunInteractiveModeAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Chuncker uygulaması beklenmedik bir hata nedeniyle sonlandırıldı.");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IServiceProvider ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Yapılandırma
        services.AddSingleton(configuration);

        // Event Sistemi
        services.AddSingleton<IEventPublisher, EventPublisher>();
        
        // Event Handler'ları otomatik keşfet ve kaydet
        RegisterEventHandlers(services);

        // Storage Providers
        services.AddSingleton<StorageProviderFactory>();
        services.AddSingleton<IStorageProvider, FileSystemStorageProvider>();
        services.AddSingleton<IStorageProvider, MongoDbStorageProvider>();
        
        // Amazon S3 Storage Provider (optional - configuration driven)
        // Temporarily disabled for build testing without AWS credentials
        // services.AddAWSService<IAmazonS3>();
        // services.AddSingleton<IStorageProvider, AmazonS3StorageProvider>();

        // Repositories
        services.AddSingleton<IFileMetadataRepository, FileMetadataRepository>();
        services.AddSingleton<IChunkMetadataRepository, ChunkMetadataRepository>();

        // Cache
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Monitoring
        services.AddSingleton<PerformanceMonitor>();

        // Services
        services.AddSingleton<IFileMetadataService, FileMetadataService>();
        services.AddSingleton<IChunkMetadataService, ChunkMetadataService>();
        services.AddSingleton<IChunkManager, ChunkManager>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IFileMetadataRepository, FileMetadataRepository>();
        services.AddSingleton<IProgressReporter, ConsoleProgressReporter>();

        // Command Pattern Infrastructure
        services.AddSingleton<ICommandMediator, CommandMediator>();

        // Command Handlers (new pattern)
        services.AddSingleton<ICommandHandler<UploadFileCommand, FileMetadata>, UploadFileCommandHandler>();
        services.AddSingleton<ICommandHandler<ScanDirectoryCommand, List<FileMetadata>>, ScanDirectoryCommandHandler>();
        services.AddSingleton<ICommandHandler<DeleteFileCommand, bool>, DeleteFileCommandHandler>();
        services.AddSingleton<ICommandHandler<VerifyFileCommand, bool>, VerifyFileCommandHandler>();
        services.AddSingleton<ICommandHandler<ListFilesCommand, IEnumerable<FileMetadata>>, ListFilesCommandHandler>();
        services.AddSingleton<ICommandHandler<DownloadFileCommand, bool>, DownloadFileCommandHandler>();

        // Command Middleware (automatic ordering via IOrderedMiddleware and MiddlewareOrderAttribute)
        services.AddSingleton(typeof(ICommandMiddleware<>), typeof(ValidationMiddleware<>));
        services.AddSingleton(typeof(ICommandMiddleware<,>), typeof(ValidationMiddleware<,>));
        services.AddSingleton(typeof(ICommandMiddleware<>), typeof(LoggingMiddleware<>));
        services.AddSingleton(typeof(ICommandMiddleware<,>), typeof(LoggingMiddleware<,>));
        services.AddSingleton(typeof(ICommandMiddleware<>), typeof(PerformanceMiddleware<>));
        services.AddSingleton(typeof(ICommandMiddleware<,>), typeof(PerformanceMiddleware<,>));

        // CLI Command Handlers
        services.AddScoped<DeleteCommandLine>();
        services.AddScoped<DownloadCommandLine>();
        services.AddScoped<ListCommandLine>();
        services.AddScoped<MetricsCommandLine>();
        services.AddScoped<SeekCommandLine>();
        services.AddScoped<SeekPlusCommandLine>();
        services.AddScoped<UploadCommandLine>();
        services.AddScoped<VerifyCommandLine>();

        // MongoDB Log Exporter
        services.AddHostedService<MongoDbLogExporter>();

        // Performans optimizasyonları uygula
        Startup.ConfigureRuntimeOptimizations();
        services.OptimizeServices();
        services.AddSerilogUi(options => options.UseMongoDb(dbOptions =>
        {
            dbOptions.WithConnectionString(configuration.GetConnectionString("MongoDB"))
                .WithDatabaseName(configuration.GetSection("DatabaseSettings:DatabaseName").Value ?? "ChunckerDB")
                .WithCollectionName(configuration.GetSection("DatabaseSettings:LogCollectionName").Value ?? "Logs");
        }));

        return services.BuildServiceProvider();
    }

    private static CliApplication ConfigureCliApplication(IServiceProvider serviceProvider)
    {
        var cliApp = new CliApplication(serviceProvider)
            .WithNameAndDescription("Chuncker", "Dağıtık Dosya Depolama Sistemi");

        // Komut handler'larını ekle
        cliApp.AddCommand<DeleteCommandLine>();
        cliApp.AddCommand<DownloadCommandLine>();
        cliApp.AddCommand<ListCommandLine>();
        cliApp.AddCommand<MetricsCommandLine>();
        cliApp.AddCommand<SeekCommandLine>();
        cliApp.AddCommand<SeekPlusCommandLine>();
        cliApp.AddCommand<UploadCommandLine>();
        cliApp.AddCommand<VerifyCommandLine>();

        return cliApp;
    }

    /// <summary>
    /// Event handler'ları otomatik olarak keşfeder ve DI container'a kaydeder
    /// </summary>
    private static void RegisterEventHandlers(IServiceCollection services)
    {
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
                        // Handler'ı DI container'a kaydet
                        services.AddSingleton(interfaceType, handlerType);
                        services.AddSingleton(handlerType);
                        
                        Console.WriteLine($"Event handler kaydedildi: {handlerType.Name} -> {interfaceType.GetGenericArguments()[0].Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Assembly taranırken hata oluştu: {assembly.FullName} - {ex.Message}");
            }
        }
    }
}