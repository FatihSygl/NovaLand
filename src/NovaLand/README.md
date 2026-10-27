# NovaLand 1.0.0

## Çalıştır

Eski sürümü sistem tepsisinden tamamen kapat. ZIP'in tamamını çıkar ve
NovaLand.exe dosyasını aç. DLL ve JSON dosyaları yanında kalsın.
.NET 10 Windows Desktop Runtime x64 gerekir. Kaynak_Kodu normal kullanım
 için gerekli değildir; kaynak proje ayrıca paketlenmiştir.

## Yeni özellikler

Pano: Alt menüde pano simgesini aç. Son 10 metin ve 7 görsel tutulur. Metinler için Kopyala, görseller için Görseli kopyala
düğmesi vardır. Kopyalama sonrası istediğin uygulamada Ctrl+V kullan.
Görsellerin önizlemesi küçüktür; kopyalama orijinal çözünürlüğü kullanır.

Windows'un bilinen Ekran Görüntüleri klasörü (yeniden yönlendirilmiş veya
OneDrive'daki konumu dahil) ve Resimler/Screenshots klasörü taranır.
Başlangıçta son yedi kaydedilen SS alınır; yenileri yaklaşık dört saniyede
fark edilir. Sadece panoya kopyalanan SS/görseller anında yakalanır.
Başka bir özel klasöre kaydettiğin SS'ler otomatik alınmaz; kopyalarsan alınır.

Görsel geçmişinde çok büyük dosyalar hariç tutulur: üst sınır 20 milyon
piksel / 32 MB PNG. Metin başına sınır 1 milyon karakterdir. Geçmiş bellekte
tutulur, diske veya internete yazılmaz. Uygulama kapanınca silinir; SS
klasöründeki dosyalar yeniden açılışta tekrar listelenir. Panodaki Temizle
sadece seçili geçmişi siler, asıl SS dosyalarını veya sistem panosunu silmez.
Ayarlar'dan geçmiş takibi kapatılabilir; kapatmak mevcut geçmişi siler.

Bluetooth: Kulaklık bağlanınca küçük adada vektör kulaklık simgesi, cihaz
adı, pil simgesi ve yüzde görünür. Yüzde bilinmiyorsa — ve Pil bilgisi yok
gösterilir. Açılışta zaten bağlı ses cihazları da bir kez gösterilir.
Büyük panel otomatik açılmaz. Gösterim sekiz saniye sürer.

Pil Windows PnP/HFP özelliğinden okunur; cihazın Bluetooth adresi ve PnP
ContainerId'siyle eşleştirilir. Başka cihazın veya bilgisayarın pili kullanılmaz.
Sürücü bu özelliği sunmuyorsa pil yüzdesi ve düşük pil uyarıları çalışmaz.
Klasik Bluetooth ses cihazları desteklenir; sadece LE Audio cihazları bu
sürümde kapsam dışıdır. Ayrı sağ/sol kulaklık ve kutu yüzdeleri gösterilmez.
Pil yaklaşık 20 saniyede, bağlantı durumu yaklaşık 4 saniyede kontrol edilir.
Windows'un raporladığı değerde sürücü kaynaklı gecikme olabilir.

%20 ve altında turuncu, %10 ve altında kırmızı animasyonlu düşük pil
uyarısı ve kısa sistem sesi vardır. Eşik başına bir kez bildirilir; aynı
seviyede her okumada tekrarlanmaz. Pil %25'in üzerine yükselince eşikler
tekrar kurulur; yeniden bağlantı yeni oturum sayılır. Fiziksel şarj olma
durumu tahmin edilmez; animasyon pil seviyesinin görsel sunumudur.
Bağlantı/pil uyarıları bildirim geçmişine de kaydedilir.

Bildirimler: Alt menüdeki zil, uygulamanın son 20 kulaklık, odak ve su molası
bildirimini gösterir. Windows'un tüm uygulama bildirimleri okunmaz. Küçük
ada yeni bir bildirimi sekiz saniye gösterir. Büyük panel kendiliğinden
açılmaz. Panel zaten açıksa geçmiş güncellenir ve zil işaretlenir.

Odak sayacı tamamlanınca küçük adada Mola zamanı bildirimi, kısa ışık
tepkisi ve sistem sesi vardır. Bildirim geçmişine de eklenir.

## Korunan davranış

Ada ekranın üst kenarında durur. Fare ayrılınca sekiz saniye sonra yukarı
gizlenir. Şarkı değişince sadece küçük ada kapak, şarkı adı, kalan süre ve
hareketli göstergeyle sekiz saniye görünür. Büyük panel sadece tıklamayla
açılır. Menü simgeleri font yerine çizimdir. Dosya rafı, ses, parlaklık,
Windows başlangıcı ve su hatırlatıcısı korunur.

## Kontroller ve sınırlar

Windows x64 Release publish başarılı; XAML olay bağlantıları doğrulandı.
Pano metin kapasite/sıralama/tekrar/tam metin testleri önceki sürümde geçti.
Gerçek Windows, kulaklık, pano görsel biçimleri ve SS klasör yönlendirmesi
bu Linux ortamında çalıştırılamadı. Donanıma bağlı özellikler için gerçek
Windows çalışma testi gerekir.

## 0.4.2 — Gerçek ses çubukları

Dekoratif tekrar animasyonu kaldırıldı. Windows WASAPI render-loopback ile
varsayılan multimedya ses çıkışı örnekleri bellekte işlenir. 2048 örneklik
Hann pencereli FFT, dört bandı ölçer: 45–250 Hz, 250–1000 Hz, 1–4 kHz,
4–14 kHz. Çubuklar hızlı yükselip yumuşak düşer; sessizlikte düzleşir.

Bu kaynak uygulama ayrımı yapmaz: o çıkıştaki toplam Windows sesi ölçülür.
Mikrofon kullanılmaz, ses dosyası oluşturulmaz veya internete gönderilmez.
Ada gizlenince/duraklatınca analiz durur. Varsayılan ses çıkışı değişince
yaklaşık bir saniye içinde yeniden bağlanır. Ses sürücüsü loopback'i
engellerse sahte animasyon kullanılmaz; çubuklar sakin kalır.

NAudio 2.2.1 (MIT) eklendi; lisansı pakette. Frekans bantları 44.1/48/96 kHz
yapay sinyallerle, sessizlik, ses yüksekliği, geçersiz örnek ve sıfırlama
durumlarıyla test edildi. Gerçek Windows ses sürücüsü testi yapılmadı.

## 0.5.0 — Bildirim sırası, sabitleme, tekerlekle ses

Küçük adanın bildirimleri sıraya alınır. Öncelik: %10 pil, %20 pil, kulaklık
bağlantısı, mola, diğer hatırlatmalar, şarkı. Daha önemli bir olay mevcut
gösterimin önüne geçer; kesilen gösterim sonra yeniden sekiz saniye gösterilir.
Aynı cihazın eski durumunun yerini yeni durum alır. Kuyruktaki şarkı değişimleri
birleştirilir, en yeni parça korunur. Bekleyen şarkının adı ve kapağı başka
bir bildirimin üzerine yazılmaz. Büyük panel açıkken gösterim süresi durur.

Pano: Metin ve görsellerin yanında raptiye vardır. Turuncu raptiye sabitlenmiş
öğeyi belirtir. Sabitlenenler ayrı bölümde tutulur ve son on kotasına dahil
değildir. Tekrar raptiyeye basmak sabitlemeyi kaldırıp normal geçmişe taşır.
Temizle düğmesi yalnızca normal geçmişi temizler. Sabitlenenler yeniden
başlatmada korunur; açıkça sabitlediğin içerikler yerel olarak
%LOCALAPPDATA%\NovaIsland\PinnedClipboard klasörüne kaydedilir. Görselin
sabitlemesini kaldırınca o görselin bu uygulamaya ait kalıcı kopyası kaldırılır;
asıl ekran görüntüsü dosyasına dokunulmaz. Sabitlenmeyen geçmiş bellekte kalır.

Ses: Küçük adanın üzerinde fare tekerleği her kademede sesi %2 artırır/azaltır.
Ada kısa ses çubuğu ve yüzde gösterimine dönüşür; son hareketten iki saniye
sonra önceki görünüme döner. Bu iki saniye aktif bildirimin süresinden düşülmez.
Büyük panelde tekerlek normal kaydırma davranışını korur.

Doğrulama: Windows x64 Release publish başarılı. Kuyruk önceliği, kesilme/devam,
şarkı birleştirme, bekletme zamanı, aynı cihazın eski durumunun kaldırılması,
sabitlerin kotadan ayrılması, tekrarlı kopya, temizleme ve sabitlemeyi kaldırma
testleri geçti. Gerçek Windows UI/tekerlek/diskte görsel geri yükleme testi
bu Linux ortamında çalıştırılamadı.

## 0.5.1 — Yapılacaklar

Alt menüdeki liste simgesi Yapılacaklar sekmesini açar. Metni yazıp Enter
veya Ekle ile yeni görev eklenir. Soldaki daire görevi tamamlar ve üstünü
çizer; tekrar basmak yeniden açar. Her satırdaki çöp kutusu o görevi siler.
Bitenleri temizle yalnızca tamamlananları, Tümünü temizle tüm görevleri
kaldırır. Son silme/temizleme Geri al ile aynı oturumda geri getirilebilir.
Görevler ve tamamlanma durumları otomatik olarak
%LOCALAPPDATA%\NovaIsland\todos.json dosyasına kaydedilir; yeniden açılışta
yüklenir. Geri alma geçmişi yalnızca o oturum için tutulur.

Doğrulama: Windows x64 Release publish başarılı. Ekleme/boş giriş/uzunluk,
tamamlama UI bildirimleri, tek silme, bitenleri/tümünü temizleme, geri alma,
sıralama ve JSON kaydetme/geri yükleme testleri geçti. Windows arayüzü bu
Linux ortamında çalıştırılamadı.

## 0.6.0 — Odak, özel süre, saatli hatırlatıcı

Odak sayacı çalışırken müzik de çalıyorsa sağdaki çubuklar gerçek sistem
sesine tepki verir. Odak sayacı ve turuncu renk korunur. Odak sekmesinde
Süre alanına 1–1440 dakika yazıp Uygula veya Enter ile sayacı yeni süreye
sıfırlayabilirsin. Başlat ayrı düğmedir. Son seçilen süre kaydedilir.

Alt menüdeki saat simgesi Hatırlatıcılar sekmesini açar. Metni ve 24 saat
biçimindeki saati (ör. 17:30) yazıp Hatırlat düğmesine bas. Saat bugün
geçmişse yarına kurulur; kesin tarih satırda gösterilir. Zamanı geldiğinde
yalnızca küçük ada açılır, metin ve saatle birlikte hareketli saat simgesi
ve kısa sistem sesi verir. Açık büyük panel varsa küçük adaya geçer.
Bildirim sekiz saniye sürer ve mevcut öncelik sırasını kullanır; %10/%20
kulaklık pil uyarıları daha önceliklidir.

Hatırlatıcı listesi %LOCALAPPDATA%\NovaIsland\reminders.json dosyasında
saklanır. Bir kez hatırlatılan tekrar çalmaz. Uygulama açıkken çalışır;
uygulama kapalıyken saati geçenler yeniden açılışta yakalanır. Uyku
sırasında geçmiş saatler de uyanınca yakalanır; bilgisayarı uykudan
uyandırmaz. Windows görev zamanlayıcısına kayıt eklenmez.

Doğrulama: Windows x64 Release publish başarılı. Özel süre doğrulaması,
saat çözümleme, bugün/yarın, gecikmiş hatırlatma, tek seferlik tetikleme ve
JSON kalıcılık testleri geçti. Odak ses kapısı ve XAML olay bağlantıları
kontrol edildi. Windows üzerinde gerçek animasyon/alarm testi yapılamadı.

## 0.6.1 — Uyarı görünümü ve görev tıklama alanı

Yapılacakların daire düğmesi 28x28 DIP tıklama alanı kullanır. Dairenin
ortası, kenarı ve düğmenin tamamı tıklanabilir; artık ince çizgiyi tutturmak
gerekmez. Tamamlama/yeniden açma ve tüm önceki özellikler korunmuştur.

Derleme başarılı. XAML yapısı ve daire/grid hit-test arka planları doğrulandı. Gerçek Windows görünüm/tıklama testi yapılmadı.

## 0.6.2 — Girişler ve Odak yerleşimi

Hatırlatıcılar ayrı sekme yerine Odak ekranının altındadır. Hatırlatıcı ekle
düğmesiyle form açılır; metin ve saat/dakika alanları görünür. Saat alanları
oklarla, fare tekerleğiyle veya doğrudan yazarak seçilebilir. Bugün/Yarın
önizlemesi seçilen zamanın hangi güne kurulacağını gösterir. Hatırlat ile
kaydedince form kapanır; x ile vazgeçmek mevcut hatırlatıcıları değiştirmez.
Form açıkken alan biraz genişler, uzun listeler aynı panelde kaydırılır.

Odak süresi yuvarlatılmış bir kontrol içinde -5/+5 düğmeleriyle veya doğrudan
yazılarak seçilir. Ayarla seçilen süreye sıfırlar; Başlat sayacı başlatır.
Başlat, süre kısayolları ve sıfırlama aynı sıcak nötr renklerle düzenlendi.

Görev ekleme tek parça koyu ve yuvarlatılmış giriş alanı ile küçük + düğmesi
kullanır. Enter ile ekleme, tam daire tıklama alanı, çizme, silme ve geri alma
korunmuştur. Ham Windows metin kutuları yerine ortak giriş/düğme şablonları
kullanılır. Önceki görevler, hatırlatıcılar ve ayarlar korunur.

Windows x64 Release publish ve XAML kaynak/olay kontrolleri başarılıdır.
Gerçek Windows görsel/etkileşim testi bu Linux ortamında yapılamadı.

## 0.6.3 — Giriş ve bağlantı regresyon düzeltmeleri

Özel TextBox ControlTemplate kaldırıldı; metin, imleç, seçim ve klavye
girişi Windows/WPF'nin standart kontrolüne bırakıldı. Yuvarlatılmış görünüm
dış çerçeveyle korunur. Boş alan yazıları tıklamayı engellemez; alana
tıklama açıkça klavye odağı verir. Hatırlatıcı metni, saat/dakika, odak süresi
ve görev girişi bu ortak düzeltmeyi kullanır.

Bluetooth taraması artık bir cihazdaki hata yüzünden tüm bağlantı listesini
yarım bırakmaz. Bilinen ses cihazları yeniden bağlanmada tekrar sınıflandırma
beklemez. Cihaz sorguları zaman aşımıyla sınırlıdır; pil okuması en fazla
450 ms beklenir ve gerekirse önbellek/bilinmeyen pil kullanılır. Bağlantı
ve ayrılma takibi pil bilgisinin bulunmasına bağlı değildir.

Aynı kulaklığın bağlantısı hızla kesilirse son durum eski gösterimin yerini
alır ve yeni sekiz saniyelik süre başlar. Büyük panel açık bırakılmışsa
bağlantı bildirimi küçük adaya geçer; bir metin alanında yazarken veya
dosya seçerken yazmayı bölmez, bekleyen son durum panel kapanınca gösterilir.
Kulaklık simgesi bağlantı/ayrılmada kısa bir büyüme animasyonu kullanır.

Beklenmeyen cihaz sorgu hataları yerel diagnostics.log dosyasına sınırlı
olarak yazılır; cihaz adı, pano metni ve tarayıcı adresleri yazılmaz.
Konum: %LOCALAPPDATA%\NovaIsland\diagnostics.log.

Derleme; bağlantı/ayrılma/yeniden bağlanma, pilsiz bağlantı, pil güncellemesi,
düşük pil eşikleri, aynı cihazın son durumu ve öncelik testleri geçti.
Standart metin kontrolü ve şeffaf yer tutucu yapısı kontrol edildi.
Gerçek Windows arayüzü veya fiziksel kulaklıkla çalışma testi yapılamadı.

## 0.6.4 — Karşılama ve Windows açılış selamlaması

- Bu sürümün ilk açılışında yalnızca bir kez küçük adada Hoş geldin mesajı.
- Windows oturum açılışında otomatik başlatıldığında yerel saate göre: 05–12 Günaydın, 12–18 İyi günler, 18–22 İyi akşamlar, 22–05 İyi geceler.
- Güneş/ay simgesi, yumuşak giriş ve ışık halkası; 8 saniyelik mevcut bildirim sırası kullanılır. Kulaklık, hatırlatıcı ve düşük pil bildirimleri önceliklidir. Normal elle açılışlarda tekrar selamlamaz.
- Otomatik başlangıç açıksa güncel exe yolu yeniden kaydedilir; kapalıysa kendiliğinden açılmaz. Klasörü kalıcı yerine taşıyıp uygulamayı bir kez oradan açın. Windows Görev Yöneticisi başlangıç uygulamaları listesinden devre dışı bırakılmışsa ayrıca oradan etkinleştirin.
- Önceki sürümün giriş, pano, odak ve Bluetooth düzeltmeleri korunur.

## 0.6.5 — Sosyal medya bağlantısı kaldırıldı

Instagram/Twitter uyarıları, animasyonları ve tarayıcı bağlantısı kaldırıldı.
Uygulama artık tarayıcıdan veri almak için yerel bir port açmaz. Eklenti ve
kurulum dosyaları paketten çıkarıldı. Önceden tarayıcıya kurulmuş NovaLand
eklentisi tarayıcının eklentiler sayfasından Kaldır düğmesiyle silinebilir.
Karşılama, otomatik açılış selamlaması ve diğer bildirimler korunur.

## 0.6.6 — Fareyle açılma ve gizlenme

Gizli küçük adanın görünür kenarında 150 ms durunca ada görünür.
Fare adanın üzerindeyken açık kalır; çekilince 8 saniye beklemeden
gizlenme animasyonu başlar. Şeffaf kenar/gölge alanları yerine adanın
gövdesi izlenir. Anlık değmeler açılmayı tetiklemez.
Şarkı/kulaklık/hatırlatıcı gibi otomatik olaylar kendi 8 saniyelik
bildirim sırasını korur. Ses göstergesi bitince fazladan 8 saniye beklemez.
Tıklanarak açılmış büyük panel ve metin girişi korunur. Otomatik gizleme
kapalıysa ada gizlenmez.

## 0.6.7 — Beyaz araç ipucu kutusu

Küçük adanın ve kulaklık pil göstergesinin fareyle bekleyince açılan
araç ipuçları kaldırıldı. Panel düğmelerinin araç ipuçları koyu zemin,
açık yazı ve yuvarlatılmış kenarlara uyarlandı. Fareyle açılma/gizlenme
ve otomatik bildirim süreleri değişmedi.

## 0.7.0 — Site sesleri ve boşta tarih-saat

Kontroller ekranı medya oynatıcısı bulunan web sekmelerini ayrı ses
sürgüleriyle listeler. YouTube, Kick ve diğer HTML video/audio oynatıcıları
aynı Chromium eklentisi üzerinden kontrol edilir. Sistem sesi, Spotify ve
diğer sekmeler değişmez. Aynı sitedeki iki sekmenin mevcut sesleri ayrı
ayarlanabilir; son seçilen seviye site bazında yeni oynatıcılara uygulanır.
Eklenti kurulumu ve kişisel kodla eşleştirme için SITE_SESI_KURULUM.txt.
Daha önce kaldırılan sosyal medya uyarısı geri eklenmemiştir.

Bağlantı yalnızca IPv4 loopback üzerinde 47864 portunda, kurulum başına
üretilen 256 bit anahtarla çalışır. Gelen isteklerin kökeni ve boyutu
sınırlanır. Komutlar tarayıcı profili ve geçerli sekme kimliğiyle ayrılır;
sekme kapanışı/yön değiştirmesinde eski komutlar uygulanmaz. Görünmeyen
sekme zamanlayıcıları yavaşlarsa oynatıcı durumu doğrudan yenilenir.
Oynatıcı yokken hızlı sorgu döngüsü durur; native arayüzü yalnızca
Kontroller açıkken yenilenir.

Boşta küçük adada sıcak nötr tonlarda takvim rozeti, Türkçe tarih/gün ve
belirgin bir dijital saat görünür. Sonsuz animasyon eklenmemiştir. Medya,
odak ve bildirim görünümü; 150 ms fareyle açılma; fare çekilince gizlenme;
koyu araç ipuçları korunur. Kulaklık/mouse için yeni özellik eklenmedi.

Doğrulama: Windows x64 Release derlemesi; Linux üzerinde gerçek TCP/HTTP
bağlantısıyla kimlik doğrulama, köken reddi, sekme/profil ayrımı, komut
onayı, hızlı sürgü değişimleri ve kapanan sekmeler; JS medya öğesi ve
çoklu çerçeve testleri; XAML olay ve önceki servislerin korunması kontrolleri.
Gerçek Windows arayüzü, Chrome/Opera/Edge ve canlı YouTube/Kick üzerinde
uçtan uca test bu ortamda yapılamadı. Web Audio ve kapalı/erişilemeyen
oynatıcılar kapsam dışıdır.

## 0.7.1 — İnce koyu kaydırma çubukları

Varsayılan beyaz geniş kaydırma çubuğu yerine şeffaf zeminde 4 DIP
yuvarlatılmış gri tutamak kullanılır. Oklar kaldırıldı; tutamak sürükleme,
fare tekerleği ve boş alana tıklayarak sayfa kaydırma korunur. Üzerine
gelince ve sürüklerken tutamak belirginleşir. Stil tüm panellere uygulanır.
Site ses eklentisi ve diğer işlevler değişmedi.

## 0.7.2 — Daha kompakt ve tutarlı arayüz

Kaydırma tutamağı 2 DIP genişliğinde, 28 DIP sabit uzunluktadır. İçeriğin
uzunluğuna göre büyümez; sürükleme, tekerlek ve sayfa kaydırma korunur.
Kontrollerde daha küçük nötr ses kartları, ince sürgüler, ayrı parlaklık
kartı ve açılır Tarayıcı bağlantısı bölümü kullanılır. Bağlantı kodu
bu bölümdeki Kodu kopyala düğmesindedir. Ayarlar gruplanmış satırlar ve
anahtarlarla sunulur. Pano/dosya/görev/bildirim başlıkları ve yüzeyleri
aynı tasarım diline uyarlandı. İlk müzik sayfası korunmuştur.

Eklenti ve C# işlevleri değişmedi. Metin girişleri standart WPF
kontrolleri olarak kaldı; mevcut giriş ve olay bağlantıları korundu.
Windows x64 derleme başarılı; gerçek Windows görsel/etkileşim testi
bu ortamda yapılamadı.

## 0.8.0 — Taşınabilir ada ve kenara yerleşme

Adanın üst başlığındaki yazı/boş alandan basılı tutup sürükleyin. Tıklama
ve sürükleme ayrıdır; düğmeler ve girişler sürüklemeyi başlatmaz. Fareyi
bırakınca yakındaki ekran kenarına yerleşir; ortada serbest kapsül olur.

- Üst, sağ, sol ve alt kenarlar desteklenir. Yaslandığı taraf düzleşir;
  serbest konumda dört köşe yuvarlaktır. Gizlenme o kenarın yönündedir.
- Sağda panel sola, altta yukarı doğru büyür. Serbest konumda büyük panel
  çalışma alanında tutulur, küçülünce kayıtlı başlık konumuna döner.
- Ortada ada kaybolmaz; küçük kapsül olarak görünür. Kenardayken önceki
  150 ms fareyle gösterme ve fare çekilince gizlenme davranışı korunur.
- Büyük adanın üst satırında kilit ve konumu sıfırlama simgeleri bulunur.
  Kilit yalnızca sürüklemeyi kapatır; müzik ve diğer kontroller çalışır.
- Sıfırlama birincil ekranın üst ortasına döndürür. Kilit tercihi korunur.
  Tepsi menüsündeki Konumu sıfırla da kurtarma için kullanılabilir.
- Konum, ekran adı ve kilit yerel ayarlara kaydedilir. Kayıtlı ekran yoksa
  birincil ekrana dönülür. Negatif koordinatlı ekranlar desteklenir.
- Sürükleme sırasında bildirim süresi duraklar; bırakınca devam eder.
  Escape sürüklemeyi iptal eder.

Windows x64 Release derlemesi, dört yönlü yerleşim/geometri, panel
büyüme/küçülme, negatif ekran koordinatları, eski ayarların taşınması
ve kalıcılık testleri başarılıdır. Gerçek Windows sürükleme, farklı DPI
kullanan çoklu ekranlar ve görsel yerleşim bu ortamda test edilemedi.
Site sesi eklentisi değişmedi, yeniden kurulması gerekmez.


## 0.8.1 — Ana sayfada site sesi ve büyük görsel önizlemesi
Müzik sayfasında bağlı medya sekmeleri için kompakt Site sesi kartı bulunur.
Oklarla sekmeler arasında geçilir; seçili sekme kapanmadıkça seçim korunur.
Aynı siteden birden fazla sekme varsa her biri ayrı numarayla seçilir.
Site sürgüsü yalnızca seçili sekmeyi, üstteki mevcut sürgü sistem sesini değiştirir.
Hiç medya sekmesi yoksa kart gizlenir. Tüm site ayarları ve eşleştirme Kontroller'de kalır.
Mevcut tarayıcı eklentisini yeniden kurmak gerekmez.
Pano > Görseller'de küçük resme tıklayarak büyük önizleme açılır.
Sığdır ile tam resim, %100 ile piksel boyutunda kaydırılabilir görüntü gösterilir.
Kopyala, X ve Esc desteklenir. Sabitlenmiş görseller ve ekran görüntüleri de desteklenir.
Önizleme açıkken ada kapanmaz ve bildirim sırası bekletilir.


## 0.8.2 — İnce site sesi ve 10 öğelik pano
Ana sayfanın site sesi alanı sadeleştirildi: küçük hoparlör simgesi,
site adı, yüzde ve ince sürgü. Tek sekmede seçim okları/sayaç gizlenir.
Birden fazla sekmede oklar ve küçük sıra göstergesi görünür.
Metin ve görsel geçmişi ayrı ayrı son 10 öğeyi tutar. Sabitler sayılmaz.
İlk ekran görüntüsü taraması da son 10 dosyayı alır.
Görseller sıkıştırılmış PNG ve küçük önizleme olarak tutulur; büyük görüntü
sadece önizlemeyi açınca çözülür. Bellek etkisi görsel boyutuna bağlıdır.


## 0.8.3 — Sade sayaç ve 7 görsel
Güncel sınırlar: son 10 metin, son 7 görsel; sabitlenenler hariç.
Başlangıç ekran görüntüsü taraması son 7 dosyayı alır.
Ayarlar simgesi daha düzenli bir dişli; sayaç sekmesi sade bir saat simgesi.
Sayaçta başlat/duraklat, küçük sıfırlama ve tek bir dakika alanı bulunur.
Dakikayı yazıp Enter veya onay simgesine bas.
Hazır 25/5/50 dakika seçenekleri Hazır süreler açılır alanında korunur.


## 0.9.0 — Kontroller: cihazlar, ses çıkışı ve ekran
Site sesleri yalnızca Müzik sayfasındadır. Tarayıcı bağlantısı ve
eşleştirme kodu Ayarlar > Tarayıcı bağlantısı alanına taşındı.
Kontroller'de bağlı Bluetooth ses cihazlarının adı ve Windows bildiriyorsa
pili görünür. Pil bilinmiyorsa Pil bilgisi yok yazılır. Yeni tarama yok;
mevcut bağlantı/pil taraması kullanılır, bildirim öncelikleri korunur.
Ses çıkışı alanında etkin hoparlör, kulaklık, USB ve HDMI çıkışları listelenir.
Bir satıra basınca Windows varsayılan konsol ve multimedya çıkışı değişir.
Görüşme çıkışı değiştirilmez. Özel cihaz seçmiş uygulamalar kendi çıkışını
kullanmaya devam edebilir. Başarı gerçek varsayılan cihaz okunarak doğrulanır.
Windows uyumluluk arayüzü desteklenmezse anlaşılır hata ve Windows ses
ayarları kısayolu vardır. Mikrofonlara erişilmez. Parlaklık kontrolü korunur.
Çıkış listesi yalnızca Kontroller açıkken yenilenir. Aynı kalan satırlar
yeniden oluşturulmaz. Ayar değişimi uygulama başlangıcında yapılmaz.
Sayaç sıfırlama simgesi belirgin bir dönüş oku olarak düzeltildi.
Metin geçmişi 10, görsel geçmişi 7; sabitlenenler hariç.

Teknik kaynaklar: Windows MMDevice API (Microsoft Learn) ve PolicyConfig
arayüz sırası için AudioDeviceCmdlets projesinin IPolicyConfig tanımı.
https://learn.microsoft.com/en-us/windows/win32/coreaudio/mmdevice-api
https://github.com/frgnca/AudioDeviceCmdlets/blob/master/SOURCE/IPolicyConfig.cs


## 0.9.1 — Uygulama ve site sesleri birlikte
Müzik sayfasındaki küçük ses kartı artık masaüstü uygulamalarını da gösterir.
Spotify, Discord, VLC, oyunlar ve Windows ses oturumu oluşturan diğer
uygulamalar, sitelerle aynı oklarla seçilir. Site/Uygulama etiketi ayırır.
Uygulamayı bir kez ses çalacak şekilde kullan; Windows'ta ses oturumu
oluşunca yaklaşık iki saniyede görünür. Oturumu açık kalan duraklatılmış
uygulamalar da görünür. Kapalı/ses oturumu olmayan programlar gösterilmez.
Uygulamalar için tarayıcı eklentisi gerekmez. Site bazında ayrım için gerekir.
Aynı işlem adına ait ses oturumları tüm etkin çıkışlarda tek başlıkta
toplanır ve birlikte ayarlanır. Birden çok ses oturumunda gösterge, sessiz
olmayan oturumların en yüksek değeridir; sürgü hepsine seçilen değeri verir.
Tarayıcı uygulamasını seçmek tarayıcının toplam sesini; site seçmek yalnızca
o sekmenin oynatıcısını etkiler. İkisi birlikte duyulan sesi belirler.
Komutlar cihaz + oturum örneği + işlem kimliği ile tam olarak hedeflenir;
kapanan oturum yerine yeni oturuma veya başka uygulamaya uygulanmaz.
Sürgü komutları sırayla işlenir, ara değerler birleştirilir.
Sistem bildirim sesleri ve Nova'nın kendi oturumu listelenmez. Mikrofon
sesi okunmaz. Exclusive-mode çıkışlar Windows oturum mikserine uymayabilir.
Tarama sadece geniş Müzik sayfası açıkken iki saniyede bir yapılır.
Windows'un ses karıştırıcısı gibi oturum sesini değiştirir; uygulamanın
kendi oynatıcı sürgüsünün görünümü aynı kalabilir.
Kaynak: https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nn-audioclient-isimpleaudiovolume


## 0.9.2 — Kulaklık bağlıyken Kontroller açılışı
Pil göstergesinin Value bağlaması açıkça OneWay yapıldı. WPF RangeBase
Value özelliği varsayılan olarak çift yönlüdür; salt okunur BatteryValue
özelliğiyle kullanılması çalışma zamanında hata oluşturabilir. Kulaklık
bağlıyken oluşturulan satırda bu hatalı bağlama kaldırıldı.
Pil bilinmiyorsa gösterge gizlenir; diğer kontroller ve özellikler korunur.
Yakalanmamış yönetilen hataların türü/HResult değeri tanılama dosyasına
yazılır; uygulama genelinde hataları yutan bir işleyici eklenmedi.
Tanılama dosyası: %LOCALAPPDATA%\NovaIsland\diagnostics.log
Kaynak: https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.primitives.rangebase.value


## 0.9.3 — Bağlı kulaklıkta bildirimle aynı pil tasarımı
Kontroller'deki düz sayı/çizgi yerine bağlantı bildirimindeki pil
çerçevesi, renkli doluluk ve yüzde birlikte gösterilir.
Kontroller açıldığında 650 ms dolum animasyonu bir kez oynar; sürekli
animasyon yoktur. Normal pil yeşil, %20 ve altı turuncu, %10 ve altı
kırmızıdır. Bilinmeyen pil boş çerçeve ve — ile gösterilir.
ProgressBar kaldırıldı; salt okunur göstergelerin bağlamaları açıkça
OneWay'dir. Bağlantı bildiriminin kendisi ve donanım sorguları değişmedi.


## 1.0.0 — NovaLand adı ve kişisel logo
Uygulamanın yeni adı NovaLand. EXE dosyası NovaLand.exe'dir.
Gönderilen kırmızı el çiziminden temizlenen logo uygulama simgesinde,
sistem tepsisinde ve Ayarlar başlığında kullanılır.
Eski ayarlar, görevler, sabitlenenler ve eşleştirme anahtarı aynı veri
konumlarından okunur. Eski sürümle aynı tek-örnek kilidi kullanılır.
Önce eski Nova Island'ı tepsiden kapat; sonra NovaLand.exe'yi aç.
Mevcut ses eklentisi çalışmaya devam eder. Eklenti yeni adı göstermek
için güncellenecekse eski kaydı kaldırmadan aynı klasörde dosyaları
yenileyip Yenile'ye bas; taşınırsa tekrar eşleştirmek gerekebilir.
Otomatik güncelleme 1.0.0 sürümünde eklendi; aşağıdaki bölüme bak.


## 1.0.0 — NovaLand ve güncellemeler

Uygulama adı NovaLand, kırmızı kıvrım logosu korunuyor.
Açılıştan yaklaşık 20 saniye sonra ve açık kaldığı sürece 6 saatte bir
FatihSygl/NovaLand GitHub Releases son kararlı sürümü kontrol edilir.
Kontrol yalnızca GitHub'a istek yapar; pano, görev, cihaz veya dosya
içeriği göndermez. İnternet yoksa uygulama normal çalışır.

Yeni sürüm arka planda indirilir. Küçük adada 8 saniyelik bildirim çıkar;
bildirime tıklayınca Ayarlar açılır. Ayarlar'dan Güncelle ve yeniden başlat
seçilince yeni sürüme geçilir. Kendiliğinden yeniden başlamaz.

Paket yalnızca bu depodan alınır; GitHub'ın bildirdiği SHA-256 ve dosya
boyutu doğrulanır. ZIP yolları, boyut limitleri, uygulama kimliği ve
sürümü kontrol edilir. Paket geçersizse çalıştırılmaz.
Yeni dosyalar %LOCALAPPDATA%\NovaIsland\Updates altında ayrı klasörde
hazırlanır. Mevcut dosyalar ve kullanıcı verileri değiştirilmez.
Yeni kopya açılışını tamamlayınca eski NovaLand kısayolları da onu açar.
Önceki kopya kurtarma için tutulur. Otomatik başlangıç açıksa yeni yola taşınır.

İlk geçiş: Eski Nova Island/Nivru sürümlerine güncelleyici geriye dönük
eklenemez. Herkes bu 1.0.0 ZIP'ini bir kere indirip NovaLand.exe'yi açmalı.
Bundan sonraki sürümler uygulamanın içinden alınabilir.
Tarayıcı eklentisi mevcut eşleştirmeyle çalışır; Chrome güvenlik modeli
nedeniyle unpacked eklenti dosyaları ayrıca güncellenir.

Kurtarma: İlk ZIP'teki NovaLand.exe'yi --recovery parametresiyle çalıştır.
Bu, kayıtlı güncel kopyaya yönlendirmeyi o açılış için atlar.
Ayarlar, görevler ve sabitlenenler eski NovaIsland veri klasöründe kalır.


## 1.1.0 — Hareket ve hava durumu (Windows)

Ana sayfadaki hava kartı Kontroller’deki geniş sahneyi açar. Şehir ara ile
sonuçlardan şehrini seç. Gece/gündüz, bulut, yağmur, kar, sis ve fırtına
vektör animasyonları Open-Meteo koşul koduna göre değişir. Şehir seçilmeden
canlı sıcaklık gösterilmez. Kaynak: https://open-meteo.com/ (CC BY 4.0).
Şehir araması geocoding-api.open-meteo.com, hava verisi api.open-meteo.com
üzerinden alınır. Konum izni/IP konum tespiti kullanılmaz; seçili şehrin
koordinatları gönderilir. Veri 15 dakika önbellekte tutulur, yenileme yalnızca
Kontroller görünürken yapılır; elle yenileme düğmesi de var. İnternet yoksa
açık oturumdaki son veri korunur ve çevrimdışı olduğu yazılır.

Sahne görünürken 24 FPS, gizlenince/sekmeyi değiştirince sıfır animasyon
tiki çalışır. Windows animasyonları kapalıysa sahne sabit görünür.
Butonlara kısa basma/hover tepkisi, daha yumuşak sekme geçişi ve daha az
sıçrayan ada boyut geçişleri eklendi. Metin kutuları native kalır.
Gerçek Windows görünümü ve güç tüketimi burada ölçülmemiştir.


## 1.2.0 — Birleşik ses alanı ve arayüz düzeni

Hava durumu ana sayfadan kaldırıldı; Kontroller'de kalır.
Müzikte tek ses alanı var: başlığa tıkla, Sistem veya bir uygulama/site seç.
Aynı anda yalnızca seçili kaynağın kaydırıcısı görünür. Sistem ilk seçenektir;
uygulama/site seçimi ses oturumlarını değiştirmez. Seçilen kaynak kapanırsa
Sistem'e döner. Kaynak listesi açıkken ada kapanmaz. Küçük adada fare tekerleği
önceki gibi sistem sesini ayarlar. Spotify oturumu için önce müzik oynatılmalı;
site bazlı kaynaklar tarayıcı eklentisini kullanmaya devam eder.

Tüm sekmelerde kart/zemin renkleri, başlıklar, menüler ve butonlar ortak bir
gri palete alındı. Alt gezinme tek yuvarlak yüzeyde birleştirildi. Müzikte
kapak büyüdü, iki ses kartı yerine tek küçük alan bırakıldı. Sekme geçişleri
yönlü ve kısa, hover tepkisi daha hafif, ayar anahtarları kayarak değişir.
Native metin kutuları, tam yuvarlak görev hedefi ve veri modelleri korunur.
Windows arayüzü burada canlı olarak görüntülenemedi; derleme ve mevcut
31 davranış kontrolü çalıştırılır.


## 1.3.0 — Birbirine bağlı çalışma alanı (Windows)

Hava durumu sayfası ve ağ sorguları kaldırıldı. Ana sayfa medya oturumu yokken
sıradaki açık görevi ve en yakın bekleyen hatırlatıcıyı gösterir. Görevden odak
başlatılabilir; aktif seans başka göreve aktarılırsa kalan süre korunur. Görev
tamamlanır veya silinirse canlı bağlantı çözülür, sayaç bağımsız devam eder.
Dosya rafı, bildirimler, kontroller ve ayarlar Diğer menüsündedir. Kopyalama
başarısı düğmede kısa bir tikle gösterilir. Panel boyutları 1.2.2 ile aynıdır.


## 1.3.1 — Sabit panel ve açık gezinme

Tüm sekmeler 440×460 DIP paneli paylaşır; sekme geçişi pencereyi büyütmez
veya taşımaz. Yerleşim ekran sınırı ve kenar payına göre uyarlanır. Tüm yedi
sekme alt menüde görünür. Bildirim geçmişi sekmesi kaldırıldı; geçici
bildirimler ve öncelik sırası 4 saniyelik gösterimle devam eder. Üst şerit
sabit marka, saat ve konum düğmelerini gösterir. Kulaklık bildirimi altta
kalıcı metin üretmez; işlem durumları dört saniyede temizlenir.
