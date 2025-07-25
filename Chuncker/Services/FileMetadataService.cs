using Chuncker.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chuncker.Applications.Events;
using Chuncker.Infsructures.Events;
using Chuncker.Infsructures.Monitoring;
using Chuncker.Interfaces;

namespace Chuncker.Services
{
    /// <summary>
    /// Dosya sistemi nesnelerinin meta verilerini yöneten servis uygulaması
    /// </summary>
    public class FileMetadataService : IFileMetadataService
    {
        private static readonly ActivitySource ActivitySource = new ActivitySource("Chuncker.FileMetadataService");
        
        private readonly IFileMetadataRepository _repository;
        private readonly ICacheService _cacheService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<FileMetadataService> _logger;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly string _basePath;
        
        /// <summary>
        /// FileMetadataService oluşturur
        /// </summary>
        /// <param name="repository">Meta veri repository'si</param>
        /// <param name="cacheService">Önbellek servisi</param>
        /// <param name="eventPublisher">Event publisher servisi</param>
        /// <param name="logger">Logger</param>
        /// <param name="performanceMonitor">Performance monitor</param>
        /// <param name="configuration">Yapılandırma</param>
        public FileMetadataService(
            IFileMetadataRepository repository,
            ICacheService cacheService,
            IEventPublisher eventPublisher,
            ILogger<FileMetadataService> logger,
            PerformanceMonitor performanceMonitor,
            IConfiguration configuration)
        {
            _repository = repository;
            _cacheService = cacheService;
            _eventPublisher = eventPublisher;
            _logger = logger;
            _performanceMonitor = performanceMonitor;
            
            _basePath = configuration.GetSection("StorageProviderSettings:FileSystemPath").Value ?? "./Storage/Files";
            
            // Temel dizinin var olduğundan emin ol
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                _logger.LogInformation("Temel dizin oluşturuldu: {BasePath}", _basePath);
            }
        }
        
        /// <summary>
        /// Verilen dizindeki tüm dosya ve klasörleri tarar ve meta verilerini kaydeder
        /// </summary>
        /// <param name="path">Taranacak dizin yolu</param>
        /// <param name="recursive">Alt dizinleri de tarayıp taramayacağı</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan nesnelerin meta verileri</returns>
        public async Task<List<FileMetadata>> ScanDirectoryAsync(string path, bool recursive, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Dizin taranıyor: {Path}, Recursive: {Recursive}, CorrelationId: {CorrelationId}", 
                path, recursive, correlationId);
            
            var result = new List<FileMetadata>();
            
            // Yolu tam yol haline getir
            string fullPath = Path.GetFullPath(path);
            
            // Dizinin var olduğundan emin ol
            if (!Directory.Exists(fullPath))
            {
                _logger.LogWarning("Dizin bulunamadı: {FullPath}, CorrelationId: {CorrelationId}", 
                    fullPath, correlationId);
                return result;
            }
            
            try
            {
                // Önce mevcut dizinin meta verisini al veya oluştur
                var directoryMetadata = await GetOrCreateObjectMetadataAsync(fullPath, correlationId, cancellationToken);
                
                if (directoryMetadata != null)
                {
                    result.Add(directoryMetadata);
                    
                    // Tüm dosyaları işle
                    foreach (var file in Directory.GetFiles(fullPath))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogWarning("Dizin tarama işlemi iptal edildi: {FullPath}, CorrelationId: {CorrelationId}", 
                                fullPath, correlationId);
                            break;
                        }
                        
                        var fileMetadata = await GetOrCreateObjectMetadataAsync(file, correlationId, cancellationToken);
                        if (fileMetadata != null)
                        {
                            fileMetadata.ParentId = directoryMetadata.Id;
                            await UpdateAsync(fileMetadata, correlationId, cancellationToken);
                            result.Add(fileMetadata);
                        }
                    }
                    
                    // Recursive ise, alt dizinleri de işle
                    if (recursive)
                    {
                        foreach (var directory in Directory.GetDirectories(fullPath))
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _logger.LogWarning("Dizin tarama işlemi iptal edildi: {FullPath}, CorrelationId: {CorrelationId}", 
                                    fullPath, correlationId);
                                break;
                            }
                            
                            var subDirectoryMetadata = await ScanDirectoryAsync(directory, true, correlationId, cancellationToken);
                            
                            // İlk öğeyi al (dizinin kendisi) ve ebeveyn ID'sini ayarla
                            if (subDirectoryMetadata.Any())
                            {
                                var subDirectory = subDirectoryMetadata.First();
                                subDirectory.ParentId = directoryMetadata.Id;
                                await UpdateAsync(subDirectory, correlationId, cancellationToken);
                            }
                            
                            result.AddRange(subDirectoryMetadata);
                        }
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dizin taranırken hata oluştu: {FullPath}, CorrelationId: {CorrelationId}", 
                    fullPath, correlationId);
                throw;
            }
        }
        
        /// <summary>
        /// Verilen yoldaki dosya veya klasörün meta verisini getirir, yoksa oluşturur
        /// </summary>
        /// <param name="path">Dosya veya klasör yolu</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Nesnenin meta verisi</returns>
        public async Task<FileMetadata> GetOrCreateObjectMetadataAsync(string path, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // Yolu tam yol haline getir
            string fullPath = Path.GetFullPath(path);
            
            // Önce önbellekten kontrol et
            var cacheKey = $"fileobject:{fullPath}";
            var metadata = await _cacheService.GetAsync<FileMetadata>(cacheKey,correlationId);
            
            if (metadata != null)
            {
                _logger.LogDebug("Nesne meta verisi önbellekten alındı: {FullPath}, CorrelationId: {CorrelationId}", 
                    fullPath, correlationId);
                return metadata;
            }
            
            // Veritabanından kontrol et
            metadata = await _repository.GetByFullPathAsync(fullPath);
            
            if (metadata != null)
            {
                // Önbelleğe ekle
                await _cacheService.SetAsync(cacheKey, metadata, TimeSpan.FromMinutes(30), correlationId);
                return metadata;
            }
            
            // Dosya veya klasör var mı kontrol et
            bool isFile = File.Exists(fullPath);
            bool isDirectory = Directory.Exists(fullPath);
            
            if (!isFile && !isDirectory)
            {
                _logger.LogWarning("Dosya veya klasör bulunamadı: {FullPath}, CorrelationId: {CorrelationId}", 
                    fullPath, correlationId);
                return null;
            }
            
            // Yeni meta veri oluştur
            metadata = new FileMetadata
            {
                Id = Guid.NewGuid().ToString(),
                FullPath = fullPath,
                Name = Path.GetFileName(fullPath),
                Type = isFile ? FileSystemObjectType.File : FileSystemObjectType.Directory,
                CreatedAt = isFile ? File.GetCreationTime(fullPath) : Directory.GetCreationTime(fullPath),
                ModifiedAt = isFile ? File.GetLastWriteTime(fullPath) : Directory.GetLastWriteTime(fullPath),
                UpdatedAt = isFile ? File.GetLastWriteTime(fullPath) : Directory.GetLastWriteTime(fullPath),
                IsIndexed = false,
                CorrelationId = correlationId
            };
            
            // Dosya ise boyut ve uzantı bilgilerini ekle
            if (isFile)
            {
                var fileInfo = new FileInfo(fullPath);
                metadata.Size = fileInfo.Length;
                metadata.Extension = Path.GetExtension(fullPath).ToLowerInvariant();
                metadata.ContentType = GetContentType(metadata.Extension);
                metadata.Status = FileStatus.Completed; // Dosya sisteminde var olan bir dosya için
                
                // Dosya içeriğinin checksum'ını hesapla
                try 
                {
                    using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                    using var sha256 = SHA256.Create();
                    var hashBytes = sha256.ComputeHash(fileStream);
                    metadata.Checksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    _logger.LogDebug("Dosya checksum hesaplandı: {FullPath}, Checksum: {Checksum}, CorrelationId: {CorrelationId}",
                        fullPath, metadata.Checksum, correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Dosya checksum hesaplanırken hata oluştu: {FullPath}, CorrelationId: {CorrelationId}",
                        fullPath, correlationId);
                    // Hata durumunda checksum boş kalabilir, işlem devam etmeli
                }
            }
            
            try
            {
                // Veritabanına kaydet
                await _repository.AddAsync(metadata, correlationId, cancellationToken);
                
                // Önbelleğe ekle
                await _cacheService.SetAsync(cacheKey, metadata, TimeSpan.FromMinutes(30), correlationId);
                
                _logger.LogInformation("Yeni nesne meta verisi oluşturuldu: {FullPath}, Id: {Id}, Tip: {Type}, CorrelationId: {CorrelationId}", 
                    fullPath, metadata.Id, metadata.Type, correlationId);
                
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nesne meta verisi oluşturulurken hata oluştu: {FullPath}, CorrelationId: {CorrelationId}", 
                    fullPath, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Verilen ID'ye sahip nesnenin meta verisini getirir
        /// </summary>
        /// <param name="id">Nesne ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Nesnenin meta verisi</returns>
        public async Task<FileMetadata> GetByIdAsync(string id, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // Önce önbellekten kontrol et
            var cacheKey = $"fileobject:id:{id}";
            var metadata = await _cacheService.GetAsync<FileMetadata>(cacheKey, correlationId);
            
            if (metadata != null)
            {
                _logger.LogDebug("Nesne meta verisi önbellekten alındı: {Id}, CorrelationId: {CorrelationId}", 
                    id, correlationId);
                return metadata;
            }
            
            // Veritabanından al
            metadata = await _repository.GetByIdAsync(id, correlationId, cancellationToken);
            
            if (metadata != null)
            {
                // Önbelleğe ekle
                await _cacheService.SetAsync(cacheKey, metadata, TimeSpan.FromMinutes(30), correlationId);
                
                // Ayrıca yol ile de önbelleğe ekle
                await _cacheService.SetAsync($"fileobject:{metadata.FullPath}", metadata, TimeSpan.FromMinutes(30), correlationId);
            }
            
            return metadata;
        }

        /// <summary>
        /// Verilen yola sahip nesnenin meta verisini getirir
        /// </summary>
        /// <param name="path">Nesne yolu</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Nesnenin meta verisi</returns>
        public async Task<FileMetadata> GetByPathAsync(string path, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // Yolu tam yol haline getir
            string fullPath = Path.GetFullPath(path);
            
            // Önce önbellekten kontrol et
            var cacheKey = $"fileobject:{fullPath}";
            var metadata = await _cacheService.GetAsync<FileMetadata>(cacheKey, correlationId);
            
            if (metadata != null)
            {
                _logger.LogDebug("Nesne meta verisi önbellekten alındı: {FullPath}, CorrelationId: {CorrelationId}", 
                    fullPath, correlationId);
                return metadata;
            }
            
            // Veritabanından al
            metadata = await _repository.GetByFullPathAsync(fullPath);
            
            if (metadata != null)
            {
                // Önbelleğe ekle
                await _cacheService.SetAsync(cacheKey, metadata, TimeSpan.FromMinutes(30), correlationId);
                
                // Ayrıca ID ile de önbelleğe ekle
                await _cacheService.SetAsync($"fileobject:id:{metadata.Id}", metadata, TimeSpan.FromMinutes(30), correlationId);
            }
            
            return metadata;
        }

        /// <summary>
        /// Verilen klasör ID'sine sahip klasörün alt klasör ve dosyalarını getirir
        /// </summary>
        /// <param name="parentId">Ebeveyn klasör ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Alt nesnelerin meta verileri</returns>
        public async Task<List<FileMetadata>> GetChildrenAsync(string parentId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            // Önce önbellekten kontrol et
            var cacheKey = $"fileobject:children:{parentId}";
            var children = await _cacheService.GetAsync<List<FileMetadata>>(cacheKey, correlationId);
            
            if (children != null)
            {
                _logger.LogDebug("Alt nesneler önbellekten alındı: ParentId: {ParentId}, Sayı: {Count}, CorrelationId: {CorrelationId}", 
                    parentId, children.Count, correlationId);
                return children;
            }
            
            // Veritabanından al
            children = await _repository.GetChildrenAsync(parentId);
            
            if (children != null && children.Any())
            {
                // Önbelleğe ekle
                await _cacheService.SetAsync(cacheKey, children, TimeSpan.FromMinutes(30), correlationId);
            }
            
            return children ?? new List<FileMetadata>();
        }

        /// <summary>
        /// Nesnenin meta verisini günceller
        /// </summary>
        /// <param name="metadata">Güncellenecek meta veri</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem sonucu</returns>
        public async Task<bool> UpdateAsync(FileMetadata metadata, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Veritabanında güncelle
                var result = await _repository.UpdateAsync(metadata, correlationId, cancellationToken);
                
                if (result)
                {
                    // Önbellekten kaldır (tekrar çağrıldığında yeniden yüklenecek)
                    await _cacheService.RemoveAsync($"fileobject:{metadata.FullPath}", correlationId);
                    await _cacheService.RemoveAsync($"fileobject:id:{metadata.Id}", correlationId);
                    
                    // Ebeveyn varsa, ebeveyn klasörün alt nesnelerini de önbellekten kaldır
                    if (!string.IsNullOrEmpty(metadata.ParentId))
                    {
                        await _cacheService.RemoveAsync($"fileobject:children:{metadata.ParentId}", correlationId);
                    }
                    
                    _logger.LogInformation("Nesne meta verisi güncellendi: {Id}, {FullPath}, CorrelationId: {CorrelationId}", 
                        metadata.Id, metadata.FullPath, correlationId);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nesne meta verisi güncellenirken hata oluştu: {Id}, {FullPath}, CorrelationId: {CorrelationId}", 
                    metadata.Id, metadata.FullPath, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Nesnenin meta verisini siler
        /// </summary>
        /// <param name="id">Nesne ID'si</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>İşlem sonucu</returns>
        public async Task<bool> DeleteAsync(string id, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Önce meta veriyi al
                var metadata = await GetByIdAsync(id, correlationId, cancellationToken);
                
                if (metadata == null)
                {
                    _logger.LogWarning("Silinecek nesne bulunamadı: {Id}, CorrelationId: {CorrelationId}", 
                        id, correlationId);
                    return false;
                }
                
                // Veritabanından sil
                var result = await _repository.DeleteAsync(id, correlationId, cancellationToken);
                
                if (result)
                {
                    // Önbellekten kaldır
                    await _cacheService.RemoveAsync($"fileobject:{metadata.FullPath}", correlationId);
                    await _cacheService.RemoveAsync($"fileobject:id:{id}", correlationId);
                    
                    // Ebeveyn varsa, ebeveyn klasörün alt nesnelerini de önbellekten kaldır
                    if (!string.IsNullOrEmpty(metadata.ParentId))
                    {
                        await _cacheService.RemoveAsync($"fileobject:children:{metadata.ParentId}", correlationId);
                    }
                    
                    _logger.LogInformation("Nesne meta verisi silindi: {Id}, {FullPath}, CorrelationId: {CorrelationId}", 
                        id, metadata.FullPath, correlationId);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nesne meta verisi silinirken hata oluştu: {Id}, CorrelationId: {CorrelationId}", 
                    id, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Tüm dosya metaverilerini getirir
        /// </summary>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Dosya metaverileri listesi</returns>
        public async Task<IEnumerable<FileMetadata>> GetAllAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Tüm dosya metaverileri getiriliyor, CorrelationId: {CorrelationId}", correlationId);
                
                var allMetadata = await _repository.GetAllAsync(correlationId, cancellationToken);
                
                _logger.LogInformation("Toplam {Count} dosya metaverisi bulundu, CorrelationId: {CorrelationId}", 
                    allMetadata?.Count() ?? 0, correlationId);
                
                return allMetadata ?? new List<FileMetadata>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm dosya metaverileri getirilirken hata oluştu, CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }

        /// <summary>
        /// Yeni bir dosya metaverisi ekler
        /// </summary>
        /// <param name="entity">Eklenecek dosya metaverisi</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eklenen dosya metaverisi</returns>
        public async Task<FileMetadata> AddAsync(FileMetadata entity, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Yeni dosya metaverisi ekleniyor: {Name}, Id: {Id}, CorrelationId: {CorrelationId}", 
                    entity.Name, entity.Id, correlationId);
                
                entity.CorrelationId = correlationId;
                var result = await _repository.AddAsync(entity, correlationId, cancellationToken);
                
                // Önbelleğe ekle
                if (!string.IsNullOrEmpty(entity.FullPath))
                {
                    var cacheKey = $"fileobject:{entity.FullPath}";
                    await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30), correlationId);
                }
                
                _logger.LogInformation("Dosya metaverisi başarıyla eklendi: {Name}, Id: {Id}, CorrelationId: {CorrelationId}", 
                    result.Name, result.Id, correlationId);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya metaverisi eklenirken hata oluştu: {Name}, CorrelationId: {CorrelationId}", 
                    entity?.Name, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Dosya adı desenine göre dosyaları bulur
        /// </summary>
        /// <param name="fileNamePattern">Dosya adı deseni</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen dosya metaverileri listesi</returns>
        public async Task<IEnumerable<FileMetadata>> FindByFileNameAsync(string fileNamePattern, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Dosya adı desenine göre arama yapılıyor: {Pattern}, CorrelationId: {CorrelationId}", 
                    fileNamePattern, correlationId);
                
                // Bu metod repository'de implementation gerektirir, şimdilik basit bir yaklaşım kullanıyoruz
                var allFiles = await _repository.GetAllAsync(correlationId, cancellationToken);
                var result = allFiles?.Where(f => f.Name != null && f.Name.Contains(fileNamePattern, StringComparison.OrdinalIgnoreCase)) ?? new List<FileMetadata>();
                
                _logger.LogInformation("Dosya adı desenine göre {Count} dosya bulundu, CorrelationId: {CorrelationId}", 
                    result.Count(), correlationId);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya adı desenine göre arama yapılırken hata oluştu: {Pattern}, CorrelationId: {CorrelationId}", 
                    fileNamePattern, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Belirli bir tarih aralığında yüklenen dosyaları bulur
        /// </summary>
        /// <param name="startDate">Başlangıç tarihi</param>
        /// <param name="endDate">Bitiş tarihi</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Eşleşen dosya metaverileri listesi</returns>
        public async Task<IEnumerable<FileMetadata>> FindByDateRangeAsync(DateTime startDate, DateTime endDate, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Tarih aralığına göre arama yapılıyor: {StartDate} - {EndDate}, CorrelationId: {CorrelationId}", 
                    startDate, endDate, correlationId);
                
                // Bu metod repository'de implementation gerektirir, şimdilik basit bir yaklaşım kullanıyoruz
                var allFiles = await _repository.GetAllAsync(correlationId, cancellationToken);
                var result = allFiles?.Where(f => f.CreatedAt >= startDate && f.CreatedAt <= endDate) ?? new List<FileMetadata>();
                
                _logger.LogInformation("Tarih aralığına göre {Count} dosya bulundu, CorrelationId: {CorrelationId}", 
                    result.Count(), correlationId);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tarih aralığına göre arama yapılırken hata oluştu: {StartDate} - {EndDate}, CorrelationId: {CorrelationId}", 
                    startDate, endDate, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Verilen dizindeki tüm dosya ve klasörleri tarar, meta verilerini kaydeder ve isteğe bağlı olarak dosya içeriklerini işler
        /// </summary>
        /// <param name="path">Taranacak dizin yolu</param>
        /// <param name="recursive">Alt dizinleri de tarayıp taramayacağı</param>
        /// <param name="processContent">Dosya içeriklerini işleyip chunk'lara böl</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Bulunan nesnelerin meta verileri</returns>
        public async Task<List<FileMetadata>> ScanAndProcessDirectoryAsync(string path, bool recursive, bool processContent, Guid correlationId, IProgressReporter progressReporter = null, CancellationToken cancellationToken = default)
        {
            using var activity = ActivitySource.StartActivity("ScanAndProcessDirectory");
            using var performanceActivity = _performanceMonitor.StartOperation("ScanAndProcessDirectory", correlationId);
            
            activity?.SetTag("path", path);
            activity?.SetTag("recursive", recursive);
            activity?.SetTag("processContent", processContent);
            activity?.SetTag("correlationId", correlationId.ToString());
            
            performanceActivity.AddTag("path", path);
            performanceActivity.AddTag("recursive", recursive);
            performanceActivity.AddTag("processContent", processContent);

            _logger.LogInformation("Gelişmiş dizin tarama başlatıldı: {Path}, Recursive: {Recursive}, ProcessContent: {ProcessContent}, CorrelationId: {CorrelationId}", 
                path, recursive, processContent, correlationId);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new List<FileMetadata>();
            
            // Progress tracking başlat
            var progress = new ScanProgress
            {
                OperationId = correlationId,
                Status = ScanProgressStatus.Starting
            };

            if (progressReporter != null)
            {
                await progressReporter.ReportStartAsync(correlationId, 
                    $"Gelişmiş dizin tarama - {path} (Recursive: {recursive}, ProcessContent: {processContent})");
                progress.Status = ScanProgressStatus.Scanning;
                await progressReporter.ReportProgressAsync(progress);
            }
            
            // Yolu tam yol haline getir
            string fullPath = Path.GetFullPath(path);
            
            // Dizinin var olduğundan emin ol
            if (!Directory.Exists(fullPath))
            {
                _logger.LogWarning("Dizin bulunamadı: {FullPath}, CorrelationId: {CorrelationId}", 
                    fullPath, correlationId);
                return result;
            }
            
            try
            {
                // Önce mevcut dizinin meta verisini al veya oluştur
                var directoryMetadata = await GetOrCreateObjectMetadataAsync(fullPath, correlationId, cancellationToken);
                
                if (directoryMetadata != null)
                {
                    result.Add(directoryMetadata);
                    
                    // Tüm dosyaları işle
                    foreach (var file in Directory.GetFiles(fullPath))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogWarning("Gelişmiş dizin tarama işlemi iptal edildi: {FullPath}, CorrelationId: {CorrelationId}", 
                                fullPath, correlationId);
                            break;
                        }
                        
                        try
                        {
                            // Dosya içeriğini analiz et (chunk işleme dahil)
                            var fileMetadata = await AnalyzeFileContentAsync(file, processContent, correlationId, cancellationToken);
                            if (fileMetadata != null)
                            {
                                fileMetadata.ParentId = directoryMetadata.Id;
                                await UpdateAsync(fileMetadata, correlationId, cancellationToken);
                                result.Add(fileMetadata);

                                // Progress güncelle
                                if (progressReporter != null)
                                {
                                    progress.UpdateFileProcessed(file, fileMetadata.Size ?? 0, fileMetadata.ChunkCount);
                                    progress.Status = ScanProgressStatus.Processing;
                                    progress.ElapsedTime = stopwatch.Elapsed;
                                    progress.FilesPerSecond = progress.ProcessedFiles / stopwatch.Elapsed.TotalSeconds;
                                    progress.BytesPerSecond = progress.ProcessedBytes / stopwatch.Elapsed.TotalSeconds;
                                    await progressReporter.ReportProgressAsync(progress);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Dosya işleme hatası: {FilePath}, CorrelationId: {CorrelationId}", 
                                file, correlationId);
                            
                            if (progressReporter != null)
                            {
                                progress.AddError($"Dosya hatası: {Path.GetFileName(file)} - {ex.Message}");
                                await progressReporter.ReportProgressAsync(progress);
                            }
                        }
                    }
                    
                    // Recursive ise, alt dizinleri de işle
                    if (recursive)
                    {
                        foreach (var directory in Directory.GetDirectories(fullPath))
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _logger.LogWarning("Gelişmiş dizin tarama işlemi iptal edildi: {FullPath}, CorrelationId: {CorrelationId}", 
                                    fullPath, correlationId);
                                break;
                            }
                            
                            try
                            {
                                var subDirectoryMetadata = await ScanAndProcessDirectoryAsync(directory, true, processContent, correlationId, progressReporter, cancellationToken);
                                
                                // İlk öğeyi al (dizinin kendisi) ve ebeveyn ID'sini ayarla
                                if (subDirectoryMetadata.Any())
                                {
                                    var subDirectory = subDirectoryMetadata.First();
                                    subDirectory.ParentId = directoryMetadata.Id;
                                    await UpdateAsync(subDirectory, correlationId, cancellationToken);

                                    // Progress güncelle
                                    if (progressReporter != null)
                                    {
                                        progress.UpdateDirectoryProcessed(directory);
                                        await progressReporter.ReportProgressAsync(progress);
                                    }
                                }
                                
                                result.AddRange(subDirectoryMetadata);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Alt dizin işleme hatası: {DirectoryPath}, CorrelationId: {CorrelationId}", 
                                    directory, correlationId);
                                
                                if (progressReporter != null)
                                {
                                    progress.AddError($"Dizin hatası: {Path.GetFileName(directory)} - {ex.Message}");
                                    await progressReporter.ReportProgressAsync(progress);
                                }
                            }
                        }
                    }
                }
                
                stopwatch.Stop();

                // Progress tamamlanma bildirimi
                if (progressReporter != null)
                {
                    progress.Status = ScanProgressStatus.Completed;
                    progress.EstimatedTotal = progress.TotalProcessed; // Tamamlandığında gerçek sayı
                    await progressReporter.ReportProgressAsync(progress);
                    await progressReporter.ReportCompletionAsync(correlationId, progress);
                }
                
                // DirectoryScanEvent yayınla
                if (_eventPublisher != null)
                {
                    var scanEvent = new DirectoryScanEvent
                    {
                        DirectoryPath = fullPath,
                        FileCount = result.Count(r => r.Type == FileSystemObjectType.File),
                        DirectoryCount = result.Count(r => r.Type == FileSystemObjectType.Directory),
                        TotalSize = result.Where(r => r.Type == FileSystemObjectType.File).Sum(r => r.Size ?? 0),
                        ProcessedContent = processContent,
                        WasRecursive = recursive,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        ProcessedFileCount = processContent ? result.Count(r => r.Type == FileSystemObjectType.File && r.ChunkCount > 0) : 0,
                        TotalChunkCount = result.Sum(r => r.ChunkCount),
                        ErrorCount = progress?.ErrorCount ?? result.Count(r => r.Status == FileStatus.Error),
                        AnalyzedFileCount = result.Count(r => r.Type == FileSystemObjectType.File && r.IsIndexed),
                        CorrelationId = correlationId
                    };

                    await _eventPublisher.PublishAsync(scanEvent);
                    _logger.LogDebug("DirectoryScanEvent yayınlandı: {Path}, CorrelationId: {CorrelationId}", fullPath, correlationId);
                }
                
                _logger.LogInformation("Gelişmiş dizin tarama tamamlandı: {Path}, Toplam: {Count}, ProcessContent: {ProcessContent}, Süre: {ElapsedMs}ms, CorrelationId: {CorrelationId}", 
                    path, result.Count, processContent, stopwatch.ElapsedMilliseconds, correlationId);
                
                return result;
            }
            catch (Exception ex)
            {
                performanceActivity.SetFailed();
                
                _logger.LogError(ex, "Gelişmiş dizin taranırken hata oluştu: {FullPath}, CorrelationId: {CorrelationId}", 
                    fullPath, correlationId);
                
                if (progressReporter != null)
                {
                    progress.Status = ScanProgressStatus.Failed;
                    await progressReporter.ReportErrorAsync(correlationId, ex.Message);
                }
                
                throw;
            }
        }

        /// <summary>
        /// Dosya içeriğini analiz eder ve meta verilerini günceller
        /// </summary>
        /// <param name="filePath">Analiz edilecek dosya yolu</param>
        /// <param name="processContent">İçeriği chunk'lara böl</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Güncellenmiş dosya meta verisi</returns>
        public async Task<FileMetadata> AnalyzeFileContentAsync(string filePath, bool processContent, Guid correlationId, CancellationToken cancellationToken = default)
        {
            using var activity = ActivitySource.StartActivity("AnalyzeFileContent");
            using var performanceActivity = _performanceMonitor.StartOperation("AnalyzeFileContent", correlationId);
            
            activity?.SetTag("filePath", filePath);
            activity?.SetTag("processContent", processContent);
            activity?.SetTag("correlationId", correlationId.ToString());
            
            performanceActivity.AddTag("filePath", filePath);
            performanceActivity.AddTag("processContent", processContent);

            var processingStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogDebug("Dosya içerik analizi başlatıldı: {FilePath}, ProcessContent: {ProcessContent}, CorrelationId: {CorrelationId}", 
                    filePath, processContent, correlationId);
                
                // Önce standard metadata'yı al/oluştur
                var metadata = await GetOrCreateObjectMetadataAsync(filePath, correlationId, cancellationToken);
                
                if (metadata == null || metadata.Type != FileSystemObjectType.File)
                {
                    return metadata;
                }
                
                // Dosya içerik işleme (chunk'lara bölme)
                if (processContent && metadata.Size > 0)
                {
                    try
                    {
                        // ChunkManager'ı dependency injection ile al
                        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                        
                        // Bu işlem için IChunkManager gerekli ama şimdilik simüle edelim
                        _logger.LogInformation("Dosya chunk işleme simülasyonu: {FilePath}, Size: {Size}, CorrelationId: {CorrelationId}", 
                            filePath, metadata.Size, correlationId);
                        
                        // TODO: Gerçek chunk manager entegrasyonu - şimdilik simülasyon
                        var estimatedChunkCount = CalculateEstimatedChunkCount(metadata.Size ?? 0);
                        metadata.ChunkCount = estimatedChunkCount;
                        metadata.Status = FileStatus.Completed;
                        
                        _logger.LogInformation("Dosya chunk işleme tamamlandı: {FilePath}, EstimatedChunks: {ChunkCount}, CorrelationId: {CorrelationId}", 
                            filePath, estimatedChunkCount, correlationId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Dosya chunk işleme hatası: {FilePath}, CorrelationId: {CorrelationId}", 
                            filePath, correlationId);
                        metadata.Status = FileStatus.Error;
                    }
                }
                
                // Gelişmiş içerik analizi
                await PerformAdvancedContentAnalysis(metadata, filePath, correlationId);
                
                processingStopwatch.Stop();
                
                // FileDiscoveredEvent yayınla
                if (_eventPublisher != null)
                {
                    var fileEvent = new FileDiscoveredEvent
                    {
                        FileId = metadata.Id,
                        FilePath = filePath,
                        FileName = metadata.Name,
                        FileSize = metadata.Size ?? 0,
                        Extension = metadata.Extension,
                        ContentType = metadata.ContentType,
                        Checksum = metadata.Checksum,
                        WasProcessed = processContent && metadata.ChunkCount > 0,
                        ChunkCount = metadata.ChunkCount,
                        Status = metadata.Status.ToString(),
                        ParentId = metadata.ParentId,
                        Tags = metadata.Tags ?? new List<string>(),
                        ProcessingTimeMs = processingStopwatch.ElapsedMilliseconds,
                        WasAnalyzed = metadata.IsIndexed,
                        WasAlreadyIndexed = metadata.LastIndexedAt.HasValue,
                        ErrorMessage = metadata.Status == FileStatus.Error ? "Processing error occurred" : null,
                        CorrelationId = correlationId
                    };

                    await _eventPublisher.PublishAsync(fileEvent);
                    _logger.LogDebug("FileDiscoveredEvent yayınlandı: {FilePath}, CorrelationId: {CorrelationId}", filePath, correlationId);
                }
                
                return metadata;
            }
            catch (Exception ex)
            {
                performanceActivity.SetFailed();
                
                _logger.LogError(ex, "Dosya içerik analizi hatası: {FilePath}, CorrelationId: {CorrelationId}", 
                    filePath, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Dosya boyutuna göre tahmini chunk sayısını hesaplar
        /// </summary>
        /// <param name="fileSize">Dosya boyutu</param>
        /// <returns>Tahmini chunk sayısı</returns>
        private int CalculateEstimatedChunkCount(long fileSize)
        {
            // Basit chunk boyutu hesaplama (1MB default)
            const long DefaultChunkSize = 1024 * 1024; // 1MB
            
            if (fileSize <= 0) return 0;
            if (fileSize <= DefaultChunkSize) return 1;
            
            return (int)Math.Ceiling((double)fileSize / DefaultChunkSize);
        }

        /// <summary>
        /// Gelişmiş içerik analizi yapar
        /// </summary>
        /// <param name="metadata">Dosya metadata'sı</param>
        /// <param name="filePath">Dosya yolu</param>
        /// <param name="correlationId">İşlem izleme kimliği</param>
        private async Task PerformAdvancedContentAnalysis(FileMetadata metadata, string filePath, Guid correlationId)
        {
            try
            {
                // MIME type refinement (daha detaylı)
                if (string.IsNullOrEmpty(metadata.ContentType) || metadata.ContentType == "application/octet-stream")
                {
                    metadata.ContentType = GetAdvancedContentType(filePath);
                }
                
                // Dosya signature kontrolü
                metadata.Tags ??= new List<string>();
                
                // Dosya boyutuna göre etiketleme
                if (metadata.Size > 100 * 1024 * 1024) // 100MB+
                {
                    metadata.Tags.Add("large-file");
                }
                else if (metadata.Size < 1024) // 1KB-
                {
                    metadata.Tags.Add("small-file");
                }
                
                // İndeksleme durumu güncelle
                metadata.IsIndexed = true;
                metadata.LastIndexedAt = DateTime.UtcNow;
                
                _logger.LogDebug("Gelişmiş içerik analizi tamamlandı: {FilePath}, ContentType: {ContentType}, Tags: {Tags}, CorrelationId: {CorrelationId}", 
                    filePath, metadata.ContentType, string.Join(",", metadata.Tags), correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gelişmiş içerik analizi hatası: {FilePath}, CorrelationId: {CorrelationId}", 
                    filePath, correlationId);
            }
        }

        /// <summary>
        /// Gelişmiş MIME type tespiti
        /// </summary>
        /// <param name="filePath">Dosya yolu</param>
        /// <returns>MIME type</returns>
        private string GetAdvancedContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Mevcut GetContentType metodunu kullan + ek tipler
            var basicType = GetContentType(extension);
            
            // Ek content type'lar
            return extension switch
            {
                ".md" => "text/markdown",
                ".yml" or ".yaml" => "application/x-yaml",
                ".log" => "text/plain",
                ".conf" or ".config" => "text/plain",
                ".sql" => "application/sql",
                ".py" => "text/x-python",
                ".cs" => "text/x-csharp",
                ".js" => "application/javascript",
                ".ts" => "application/typescript",
                _ => basicType
            };
        }
        
        /// <summary>
        /// Dosya uzantısına göre içerik türünü (MIME) belirler
        /// </summary>
        /// <param name="extension">Dosya uzantısı</param>
        /// <returns>İçerik türü</returns>
        private string GetContentType(string extension)
        {
            return extension switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                _ => "application/octet-stream"
            };
        }

        public async Task<List<FileMetadata>> ScanDirectoryParallelAsync(string path, bool recursive, bool processContent, Guid correlationId, IProgressReporter progressReporter = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Parallel directory scan started: {Path}, Recursive: {Recursive}, ProcessContent: {ProcessContent}, CorrelationId: {CorrelationId}",
                path, recursive, processContent, correlationId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new System.Collections.Concurrent.ConcurrentBag<FileMetadata>();
            var progress = new ScanProgress();

            if (progressReporter != null)
            {
                // Initial progress report
            }

            string fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                _logger.LogWarning("Directory not found: {FullPath}, CorrelationId: {CorrelationId}",
                    fullPath, correlationId);
                return new List<FileMetadata>();
            }

            var maxDop = Environment.ProcessorCount;
            using var semaphore = new SemaphoreSlim(maxDop);

            try
            {
                var directoryMetadata = await GetOrCreateObjectMetadataAsync(fullPath, correlationId, cancellationToken);
                if (directoryMetadata != null)
                {
                    result.Add(directoryMetadata);
                }

                var files = Directory.GetFiles(fullPath);
                var fileTasks = new List<Task>();

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(cancellationToken);
                    fileTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var fileMetadata = await AnalyzeFileContentAsync(file, processContent, correlationId, cancellationToken);
                            if (fileMetadata != null)
                            {
                                fileMetadata.ParentId = directoryMetadata.Id;
                                await UpdateAsync(fileMetadata, correlationId, cancellationToken);
                                result.Add(fileMetadata);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing file in parallel: {File}, CorrelationId: {CorrelationId}", file, correlationId);
                            progress.Errors.Add($"Error processing file {file}: {ex.Message}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken));
                }

                await Task.WhenAll(fileTasks);

                if (recursive)
                {
                    foreach (var directory in Directory.GetDirectories(fullPath))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var subDirFiles = await ScanDirectoryParallelAsync(directory, recursive, processContent, correlationId, progressReporter, cancellationToken);
                        foreach(var f in subDirFiles)
                        {
                            result.Add(f);
                        }
                    }
                }

                stopwatch.Stop();
                _logger.LogInformation("Parallel directory scan completed in {ElapsedMilliseconds}ms.", stopwatch.ElapsedMilliseconds);
                return result.ToList();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Parallel directory scan cancelled: {FullPath}, CorrelationId: {CorrelationId}", fullPath, correlationId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during parallel directory scan: {FullPath}, CorrelationId: {CorrelationId}",
                    fullPath, correlationId);
                throw;
            }
        }

        public async Task<Dictionary<string, List<FileMetadata>>> FindDuplicatesByChecksumAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            using var activity = ActivitySource.StartActivity("FindDuplicatesByChecksum");
            activity?.SetTag("correlationId", correlationId.ToString());

            _logger.LogInformation("Finding and tagging duplicate files by checksum, CorrelationId: {CorrelationId}", correlationId);

            try
            {
                var allFiles = await _repository.GetAllAsync(correlationId, cancellationToken);
                var filesWithChecksum = allFiles.Where(f => f.Type == FileSystemObjectType.File && !string.IsNullOrEmpty(f.Checksum));

                var duplicates = filesWithChecksum
                    .GroupBy(f => f.Checksum)
                    .Where(g => g.Count() > 1)
                    .ToDictionary(g => g.Key, g => g.ToList());

                if (duplicates.Any())
                {
                    _logger.LogInformation("Found {DuplicateCount} sets of duplicate files. Tagging them... CorrelationId: {CorrelationId}", duplicates.Count, correlationId);
                    foreach (var set in duplicates.Values)
                    {
                        foreach (var fileMetadata in set)
                        {
                            if (fileMetadata.Tags == null)
                            {
                                fileMetadata.Tags = new List<string>();
                            }

                            if (!fileMetadata.Tags.Contains("duplicate"))
                            {
                                fileMetadata.Tags.Add("duplicate");
                                await UpdateAsync(fileMetadata, correlationId, cancellationToken);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No duplicate files found, CorrelationId: {CorrelationId}", correlationId);
                }

                return duplicates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding and tagging duplicate files by checksum, CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }
        
        /// <summary>
        /// Dosya adı veya pattern ile arama yapar
        /// </summary>
        public async Task<IEnumerable<FileMetadata>> FindFilesAsync(string pattern, Guid correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Dosya arama işlemi başlatıldı: Pattern: {Pattern}, CorrelationId: {CorrelationId}", 
                    pattern, correlationId);
                
                // Pattern wildcard içeriyor mu kontrol et
                bool isWildcardPattern = pattern.Contains("*") || pattern.Contains("?");
                
                if (isWildcardPattern)
                {
                    // Wildcard içeren pattern için dosya adı araması yap
                    return await FindByFileNameAsync(pattern, correlationId, cancellationToken);
                }
                else
                {
                    // Tam eşleşme için dosya adı araması yap
                    var exactMatches = await FindByFileNameAsync(pattern, correlationId, cancellationToken);
                    if (exactMatches.Any())
                    {
                        return exactMatches;
                    }
                    
                    // ID ile arama yapmayı dene
                    var idMatch = await GetByIdAsync(pattern, correlationId, cancellationToken);
                    if (idMatch != null)
                    {
                        return new List<FileMetadata> { idMatch };
                    }
                    
                    // Path ile arama yapmayı dene
                    var pathMatch = await GetByPathAsync(pattern, correlationId, cancellationToken);
                    if (pathMatch != null)
                    {
                        return new List<FileMetadata> { pathMatch };
                    }
                    
                    // Hiçbir şey bulunamadıysa boş liste döndür
                    return Enumerable.Empty<FileMetadata>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya arama hatası: Pattern: {Pattern}, CorrelationId: {CorrelationId}", 
                    pattern, correlationId);
                return Enumerable.Empty<FileMetadata>();
            }
        }
    }
}
