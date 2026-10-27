# NovaLand

Windows için küçük, kişisel bir masaüstü adası. Müzik, uygulama/site sesleri,
pano, görevler, hatırlatıcılar ve Bluetooth kulaklık bildirimleri tek yerde.

## İndir

[Son sürüm](https://github.com/FatihSygl/NovaLand/releases/latest) sayfasından
`NovaLand_vX.Y.Z_win-x64.zip` dosyasını indir. ZIP'in tamamını çıkar, eski
uygulamayı sistem tepsisinden kapat ve `NovaLand.exe` dosyasını çalıştır.
Windows x64 ve [.NET 10 Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) gerekir.

1.0.0 ve üstü sürümler açılıştan 20 saniye sonra ve sonra 6 saatte bir
güncellemeleri kontrol eder. Yeni sürüm arka planda indirilir; kullanıcı
Ayarlar'dan **Güncelle ve yeniden başlat** seçince uygulanır.
Pano, görev ve cihaz içerikleri sunucuya gönderilmez.

## Geliştirme

Kaynak: `src/NovaLand`. `.NET 10 SDK` ile:

```sh
dotnet publish src/NovaLand/NovaLand.csproj -c Release -r win-x64 -p:SelfContained=false -p:PublishSelfContained=false -p:PublishSingleFile=false -p:DebugType=None -o artifacts/publish
dotnet run --project tests/UpdateChecks.csproj -- artifacts/publish
```

Windows'ta tam paketi oluşturmak için:

```powershell
./scripts/Build-Release.ps1
```

## Yeni sürüm yayımlama

`src/NovaLand/NovaLand.csproj` içindeki `Version` artırılıp değişiklik
`main` dalına gönderilince GitHub Actions derler, güncelleyici kontrollerini
çalıştırır ve o sürüm daha önce yayımlanmamışsa yeni Release oluşturur.
Mevcut yayının ZIP'i değiştirilmez. Başarısız derleme veya kontrol yayını durdurur.
Pull request'lerde yalnızca derleme ve kontroller çalışır.

İlk kaynak aktarımı 1.0.0'ı yeniden yayımlamaz. Yayımlama işlemi için kişisel
token gerekmez; yalnızca akışın kendi depoya sınırlı `GITHUB_TOKEN` yetkisi kullanılır.

Bu kontroller Windows arayüzünü, gerçek Bluetooth cihazlarını ve gerçek
yeniden başlatma geçişini otomatik olarak doğrulamaz.

Ayrıntılı kullanım: [uygulama kılavuzu](src/NovaLand/README.md).

## MacBook / macOS önizlemesi

[Mac sürümü 0.1.0](https://github.com/FatihSygl/NovaLand/releases/tag/mac-v0.1.0)
Intel ve Apple Silicon için tek Universal `.app` paketi sağlar. macOS 13+
gerekir; .NET gerekmez. Yerel SwiftUI/AppKit kaynakları `macOS/` altındadır.

Bu ilk Mac sürümü Windows ile tam özellik eşitliğinde değildir. Desteklenen
özellikler, izinler ve kurulum: [Mac kılavuzu](macOS/README.md).
Mac yayınları ayrı `mac-vX.Y.Z` önizleme etiketini kullanır; Windows'un
Latest sürümü korunur. macOS/VERSION değişince Mac akışı yeni paketi yayımlar.
