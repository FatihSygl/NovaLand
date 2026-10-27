# NovaLand for macOS 0.1.0 — Önizleme

macOS 13 Ventura veya üstü. Intel ve Apple Silicon (M1/M2/M3/M4 ve sonrası)
için tek Universal uygulama. .NET, Homebrew veya ayrı bir çalışma zamanı gerekmez.

## Kurulum

NovaLand_v0.1.0_macOS_universal.zip dosyasını açın. NovaLand.app dosyasını
Applications (Uygulamalar) klasörüne taşıyıp açın. Menü çubuğundaki kırmızı
logodan uygulamayı açabilir, konumunu sıfırlayabilir veya çıkabilirsiniz.

Bu önizleme kişisel Apple Developer ID sertifikası olmadan ad-hoc imzalanır;
Apple tarafından noter onayı alınmamıştır. macOS ilk açılışı engelleyebilir.
Kaynağa güveniyorsanız açmayı denedikten sonra Sistem Ayarları > Gizlilik ve
Güvenlik > Yine de Aç yolunu kullanın. Gatekeeper'ı tamamen kapatmayın.
Resmî Apple açıklaması: https://support.apple.com/102445

## Kullanım

- Ekranın üstünde küçük ada. Üzerinde 150 ms bekleyince görünür; fare çekilince
  gizlenir. Bildirimler 8 saniye gösterilir. Tıklayınca büyük panel açılır.
- Çentikli ekranda kamera alanı için üst boşluk ayrılır. Ada sürüklenebilir;
  üst kenara yaklaşınca yapışır. Serbest konumda görünür kalır. Kilit ve
  sıfırlama düğmeleri büyük panelin sağ üstündedir.
- Yapılacaklar: görev ekleme, yuvarlağın tamamına basarak tamamlama, silme
  ve tamamlananları temizleme. Görevler bu Mac'te kalıcıdır.
- Pano: son 10 metin, 7 görsel; sabitlenenler bu sayılara dahil değildir.
  Kopyala ve sabitle/ayır düğmeleri vardır. Görsele tıklayınca büyük önizleme
  açılır; kopyalama orijinal PNG verisini kullanır. Sabitlenenler kalıcıdır,
  diğer geçmiş uygulama kapanınca silinir. Hassas pano işaretleri atlanır.
- Odak: 1–1440 dakika, başlat/duraklat/sıfırla ve 5/25/50 dakika seçenekleri.
- Hatırlatıcı: metin ve saat. Geçmiş saat yarına kurulur. Zamanı gelince
  küçük adada saat/metin ve alarm simgesi ile ses çıkar. Uygulama çalışmalı;
  uyku veya yeniden açılıştan sonra geciken hatırlatıcı bir kez gösterilir.
- Spotify / Music bağlantısı: Müzik sayfasındaki düğmeyle veya Ayarlar'dan
  açılır. macOS Otomasyon iznini onaylayın. Yalnızca açık olan Spotify/Music
  uygulamalarındaki şarkı, sanatçı, süre ve oynatma durumu okunur. Oynat/
  duraklat/önceki/sonraki düğmeleri çalışır. Şarkı değişince küçük ada görünür.
- Bluetooth bildirimleri isteğe bağlıdır. Eşleştirilmiş cihazların bağlantı
  durumunu izler; tarama, eşleştirme veya mikrofon kaydı yapmaz. Her bağlı
  Bluetooth cihazı bildirilebilir, yalnızca kulaklık filtresi yoktur.
- Bildirim sırası: hatırlatıcı > Bluetooth > odak > müzik. Birbirlerini
  silmezler; yüksek öncelikli bildirimden sonra bekleyen devam eder.
- Mac açılınca başlat: Ayarlar'dan isteğe bağlı. macOS ayrıca Giriş Öğeleri
  onayı isteyebilir. Durumu Sistem Ayarları > Genel > Giriş Öğeleri'nden kontrol edin.

## Bu ilk Mac sürümünde olmayanlar

Windows uygulamasının WPF kodu Mac'te çalışmaz; bu yerel SwiftUI/AppKit sürümüdür.
Tarayıcı sekmelerine/uygulamalara özel ses ayarı, gerçek müzik spektrum çubukları,
Bluetooth pil yüzdesi ve düşük pil uyarısı, ekran parlaklığı, Windows bildirim
geçmişi ve dosya rafı henüz yoktur. Otomatik SS klasörü takibi yoktur; panoya
kopyalanan SS/görseller pano geçmişinde görünür. Albüm kapağı yerine müzik
simgesi kullanılır. Bu özellikler varmış gibi sahte veri veya ses animasyonu gösterilmez.

Mac güncelleme denetimi Ayarlar'dan elle yapılır; Mac yayınlarını ayrı arar
ve yeni sürüm varsa indirme sayfasını açar. Arka planda indirme/kurulum yoktur.
Windows güncelleyicisi Mac paketini almaz; Mac yayınları mac-vX.Y.Z adıyla
önizleme olarak tutulur.

## Veriler ve izinler

Görevler/hatırlatıcılar/sabitlenenler ~/Library/Application Support/NovaLand
klasöründe JSON olarak saklanır. Bu yerel dosyalar şifreli değildir. Ayarlar
UserDefaults'ta saklanır. Windows ile otomatik veri eşitleme yoktur. Müzik ve
Bluetooth izni bu özellikler etkinleştirildiğinde gerekir. macOS sürümüne göre
pano okuma erişimi de istenebilir; reddedilirse pano takibi çalışmaz.

İnternete yalnızca Güncellemeleri denetle seçilince GitHub sürüm isteği yapılır;
pano, görev veya cihaz bilgileri gönderilmez. Spotify/Music okumaları yalnızca
etkinleştirildiğinde 3 saniyede, Bluetooth 5 saniyede, pano 1 saniyede kontrol
edilir. Gerçek güç/bellek tüketimi henüz ölçülmedi.

## Derleme / doğrulama

Xcode Command Line Tools olan bir Mac'te depo kökünden:
  bash macOS/build.sh

Akış iki mimariyi derler, Universal binary oluşturur, paket imzasını ve
Info.plist'i doğrular; 17 temel davranış kontrolünü çalıştırır. CI derlemesi
bir MacBook'ta manuel arayüz, çentik, AppleScript izinleri, Bluetooth veya
uyku/uyanma testi yapıldığı anlamına gelmez. Önizlemeyi önce kendi Mac'inizde deneyin.
