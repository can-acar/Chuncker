# Chuncker - Dağıtık Dosya Depolama Sistemi

## Genel Bakış

Chuncker, büyük dosyaların küçük parçalara (chunk) ayrılması, bu parçaların farklı depolama sağlayıcılarına dağıtılması ve gerektiğinde birleştirilerek dosya bütünlüğünün korunmasını sağlayan bir dağıtık dosya depolama sistemidir. .NET 8.0 Console Application olarak geliştirilmiştir.

## Temel Özellikler

- **Dinamik Parçalama:** Dosyalar boyutuna göre optimum parçalara ayrılır
- **Çoklu Depolama Desteği:** Dosya parçaları farklı depolama sağlayıcılarına dağıtılır (Dosya sistemi, MongoDB)
- **Metadata Yönetimi:** Tüm dosya ve parça bilgileri MongoDB'de saklanır
- **Önbellek Desteği:** Redis ile metadata bilgileri önbelleğe alınır
- **Sıkıştırma:** Gzip ile parçalar sıkıştırılarak depolanır
- **Event Tabanlı Mimari:** Özel event sistemi ile asenkron işlem yönetimi
- **Memory-mapped File Kullanımı:** Büyük dosyaları verimli şekilde işleme
- **Dosya Bütünlüğü:** SHA256 checksums ile dosya bütünlüğünü koruma
- **Kapsamlı Loglama:** Serilog ile MongoDB ve dosyalara loglama
- **CorrelationId Takibi:** Tüm işlemler boyunca tek bir işlem kimliği takibi
- **Grafana ve Loki Entegrasyonu:** Merkezi log yönetimi ve görselleştirme

## Kurulum ve Çalıştırma

### Gereksinimler

- .NET 8.0 SDK veya daha yeni
- MongoDB (veritabanı)
- Redis (önbellek)
- Docker ve Docker Compose (opsiyonel)

### Klasik Çalıştırma

```bash
# Projeyi klonlayın
git clone https://github.com/yourusername/chuncker.git
cd chuncker

# NuGet paketlerini yükleyin ve projeyi derleyin
dotnet restore
dotnet build

# Uygulamayı çalıştırın
dotnet run --project "./Chuncker/Chuncker.csproj"

# Veya belirli bir komutu çalıştırın
dotnet run --project "./Chuncker/Chuncker.csproj" -- upload ./test-file.txt
```

### Docker ile Çalıştırma

```bash
# Docker imajı oluşturun
docker build -t chuncker .

# Uygulamayı çalıştırın
docker run -it chuncker

# Tüm servisleri Docker Compose ile başlatın
docker-compose up -d
```

### Grafana ve Loki ile Log İzleme

```bash
# Loki, Grafana ve Promtail servislerini başlatın
docker-compose up -d

# Grafana'ya erişin
# Tarayıcıda http://localhost:3000 adresini açın
# Kullanıcı adı: admin, Şifre: admin
```

## Kullanım Örnekleri

### Dosya Yükleme
```bash
dotnet run --project "./Chuncker/Chuncker.csproj" -- upload /path/to/file.txt
```

### Dosya İndirme
```bash
dotnet run --project "./Chuncker/Chuncker.csproj" -- download file-id -o /output/path.txt
```

### Dosya Listeleme
```bash
dotnet run --project "./Chuncker/Chuncker.csproj" -- list
```

### Dosya Silme
```bash
dotnet run --project "./Chuncker/Chuncker.csproj" -- delete file-id
```

### Dosya Bütünlük Kontrolü
```bash
dotnet run --project "./Chuncker/Chuncker.csproj" -- verify file-id
```

## Mimari Tercihler

Chuncker, birçok modern mimari yaklaşımı ve tasarım desenini bir araya getirir:

### Katmanlı Mimari
- **Core Katmanı:** Temel domain modelleri ve arayüzler
- **Services Katmanı:** İş mantığını içeren servisler
- **Repositories Katmanı:** Veri erişim katmanı
- **Providers Katmanı:** Depolama sağlayıcı implementasyonları
- **Applications Katmanı:** Uygulama komutları ve event handler'ları
- **Infrastructure Katmanı:** Altyapı bileşenleri ve yardımcı sınıflar

### Tasarım Desenleri
- **Repository Pattern:** Veri erişim mantığını soyutlamak için
- **Factory Pattern:** StorageProviderFactory ile depolama sağlayıcılarını oluşturmak için
- **Strategy Pattern:** Farklı depolama stratejilerini uygulamak için
- **Dependency Injection:** Bileşenler arası gevşek bağlılık sağlamak için
- **Observer/Event Pattern:** Asenkron event işleme için
- **Decorator Pattern:** Cache mekanizması uygulamak için
- **Command Pattern:** Kullanıcı komutlarını modellemek için
- **Middleware Pattern:** Komut işleme akışına eklenebilen ara yazılımlar

### Teknoloji Seçimleri
- **.NET 8.0:** Modern ve performanslı bir platform
- **MongoDB:** Esnek ve ölçeklenebilir bir NoSQL veritabanı
- **Redis:** Yüksek performanslı önbellek çözümü
- **Serilog:** Yapılandırılabilir ve genişletilebilir loglama
- **Docker:** Kolay dağıtım ve ortam yönetimi
- **Grafana ve Loki:** Log görselleştirme ve analiz

## Performans Optimizasyonları

Chuncker, yüksek performans ve verimlilik için çeşitli optimizasyonlar içerir:

- **Memory-mapped Files:** Büyük dosyaları düşük bellek kullanımı ile işleme
- **Paralel İşleme:** Çoklu parça işlemleri için paralel çalışma
- **ArrayPool Kullanımı:** Bellek havuzu ile GC baskısını azaltma
- **Buffer Yönetimi:** Verimli I/O işlemleri için buffer optimizasyonları
- **Asenkron Programlama:** I/O bağımlı işlemlerde async/await kullanımı
- **Redis Önbellek:** Sık erişilen veriler için hızlı erişim

## Ekstra Özellikler

### Grafana ve Loki ile Log İzleme
Chuncker, Grafana ve Loki entegrasyonu sayesinde gelişmiş log görselleştirme ve analiz imkanı sunar:

- **Gerçek Zamanlı Log İzleme:** Uygulama loglarını anlık olarak görüntüleme
- **CorrelationId ile Filtreleme:** İşlem bazlı loglama ve izleme
- **Özelleştirilmiş Dashboard:** Sistem durumu ve metrikler için dashboard
- **LogQL Sorguları:** Gelişmiş log sorgulama ve filtreleme

### Dinamik Chunk Boyutu Hesaplama
Chuncker, dosya boyutuna ve sistem kaynaklarına göre optimum parça boyutu hesaplar:

- **Küçük Dosyalar:** Daha küçük parçalara ayrılır (minimum parça boyutu ile)
- **Orta Boy Dosyalar:** Dengeli parça boyutu kullanılır
- **Büyük Dosyalar:** Daha büyük parçalara ayrılır (maksimum parça boyutu ile)

### Otomatik Sıkıştırma
Ayarlanabilir sıkıştırma seviyesi ile veri depolama optimizasyonu:

- **Fastest:** Hızlı sıkıştırma, orta seviye sıkıştırma oranı
- **Optimal:** Dengeli hız ve sıkıştırma oranı (varsayılan)
- **SmallestSize:** Maksimum sıkıştırma oranı, daha yavaş işleme

## Konfigürasyon

Chuncker, `appsettings.json` dosyası veya ortam değişkenleri ile yapılandırılabilir:

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://admin:password@localhost:27017/ChunckerDB",
    "Redis": "localhost:6379"
  },
  "ChunkSettings": {
    "DefaultChunkSizeInBytes": 1048576,
    "MinChunkSizeInBytes": 262144,
    "MaxChunkSizeInBytes": 4194304,
    "CompressionEnabled": true,
    "CompressionLevel": 6
  },
  "StorageProviderSettings": {
    "DefaultProvider": "filesystem",
    "FileSystemPath": "./Storage/Files",
    "MongoDbBucketName": "chunks"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

## Lisans

MIT

## Katkıda Bulunma

1. Fork edin
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Değişikliklerinizi commit edin (`git commit -m 'Add some amazing feature'`)
4. Branch'inize push edin (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun