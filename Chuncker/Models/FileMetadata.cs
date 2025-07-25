using System;
using System.Collections.Generic;

namespace Chuncker.Models
{
    /// <summary>
    /// Dosya durumları
    /// </summary>
    public enum FileStatus
    {
        Pending,
        Processing,
        Completed,
        Error,
        Failed,
    }

    /// <summary>
    /// Dosya sistemi nesne türü
    /// </summary>
    public enum FileSystemObjectType
    {
        /// <summary>
        /// Dosya
        /// </summary>
        File,
        
        /// <summary>
        /// Klasör
        /// </summary>
        Directory
    }

    /// <summary>
    /// Dosya sistemi nesne meta verisi
    /// </summary>
    public class FileMetadata
    {
        /// <summary>
        /// Nesne kimliği
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Nesnenin tam yolu
        /// </summary>
        public string FullPath { get; set; }
        
        /// <summary>
        /// Nesnenin adı (dosya veya klasör adı)
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Dosyanın adı (backward compatibility)
        /// </summary>
        public string FileName 
        { 
            get => Name; 
            set => Name = value; 
        }
        
        /// <summary>
        /// Nesnenin türü (Dosya veya Klasör)
        /// </summary>
        public FileSystemObjectType Type { get; set; }
        
        /// <summary>
        /// Nesnenin boyutu (bayt cinsinden, klasörler için null)
        /// </summary>
        public long? Size { get; set; }
        
        /// <summary>
        /// Dosyanın boyutu (byte cinsinden) (backward compatibility)
        /// </summary>
        public long FileSize 
        { 
            get => Size ?? 0; 
            set => Size = value; 
        }
        
        /// <summary>
        /// Nesnenin oluşturulma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Nesnenin son değiştirilme tarihi
        /// </summary>
        public DateTime ModifiedAt { get; set; }
        
        /// <summary>
        /// Dosya uzantısı (dosyalar için, klasörler için null)
        /// </summary>
        public string Extension { get; set; }
        
        /// <summary>
        /// İçerik türü/MIME türü (dosyalar için)
        /// </summary>
        public string ContentType { get; set; }
        
        /// <summary>
        /// Ebeveyn klasör ID'si (kök klasör için null)
        /// </summary>
        public string ParentId { get; set; }
        
        /// <summary>
        /// İndekslenme durumu
        /// </summary>
        public bool IsIndexed { get; set; }
        
        /// <summary>
        /// Son indeksleme tarihi
        /// </summary>
        public DateTime? LastIndexedAt { get; set; }
        
        /// <summary>
        /// Alt klasör ve dosyalar (klasörler için)
        /// </summary>
        public List<FileMetadata> Children { get; set; } = new List<FileMetadata>();
        
        /// <summary>
        /// Etiketler
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
        
        /// <summary>
        /// Dosyanın SHA256 checksum değeri (dosyalar için)
        /// </summary>
        public string Checksum { get; set; }
        
        /// <summary>
        /// Dosyanın durumu (dosyalar için)
        /// </summary>
        public FileStatus Status { get; set; } = FileStatus.Pending;
        
        /// <summary>
        /// Dosyanın chunk sayısı (dosyalar için)
        /// </summary>
        public int ChunkCount { get; set; }
        
        /// <summary>
        /// Dosyanın son güncellenme tarihi
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
        
        /// <summary>
        /// Dosyayı oluşturan chunk'ların listesi (dosyalar için)
        /// </summary>
        public List<ChunkMetadata> Chunks { get; set; } = new List<ChunkMetadata>();
        
        /// <summary>
        /// İşlem izleme kimliği
        /// </summary>
        public Guid CorrelationId { get; set; }
    }
}
