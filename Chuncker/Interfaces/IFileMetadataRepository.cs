using Chuncker.Models;

namespace Chuncker.Interfaces
{
    /// <summary>
    /// Dosya sistemi nesne meta verisi repository arayüzü
    /// </summary>
    public interface IFileMetadataRepository : IRepository<FileMetadata>
    {
        /// <summary>
        /// Verilen tam yola sahip nesnenin meta verisini getirir
        /// </summary>
        /// <param name="fullPath">Tam yol</param>
        /// <returns>Nesne meta verisi</returns>
        Task<FileMetadata> GetByFullPathAsync(string fullPath);
        
        /// <summary>
        /// Verilen ebeveyn ID'sine sahip tüm nesneleri getirir
        /// </summary>
        /// <param name="parentId">Ebeveyn ID'si</param>
        /// <returns>Nesne meta verisi listesi</returns>
        Task<List<FileMetadata>> GetChildrenAsync(string parentId);
        
        /// <summary>
        /// Verilen ebeveyn yolundaki tüm nesneleri getirir
        /// </summary>
        /// <param name="parentPath">Ebeveyn yolu</param>
        /// <returns>Nesne meta verisi listesi</returns>
        Task<List<FileMetadata>> GetByParentPathAsync(string parentPath);
        
        /// <summary>
        /// Belirli bir tipteki tüm nesneleri getirir
        /// </summary>
        /// <param name="type">Nesne tipi</param>
        /// <returns>Nesne meta verisi listesi</returns>
        Task<List<FileMetadata>> GetByTypeAsync(FileSystemObjectType type);
        
        /// <summary>
        /// İndekslenmemiş tüm nesneleri getirir
        /// </summary>
        /// <returns>İndekslenmemiş nesne meta verisi listesi</returns>
        Task<List<FileMetadata>> GetNonIndexedAsync();
        
        /// <summary>
        /// Verilen etiketlere sahip tüm nesneleri getirir
        /// </summary>
        /// <param name="tags">Etiketler</param>
        /// <returns>Nesne meta verisi listesi</returns>
        Task<List<FileMetadata>> GetByTagsAsync(List<string> tags);
    }
}
