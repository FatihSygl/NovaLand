import AppKit
import SwiftUI
import ServiceManagement

final class IslandModel: ObservableObject {
    @Published var expanded = false { didSet { onLayout?() } }
    @Published var tab: IslandTab = .music
    @Published var now = Date()
    @Published var todos: [TodoItem] = []
    @Published var reminders: [ReminderItem] = []
    @Published var clipboard: [ClipboardEntry] = []
    @Published var media = MediaSnapshot()
    @Published var notice: IslandNotice?
    @Published var focus = FocusClock()
    @Published var message = ""
    @Published var musicEnabled = UserDefaults.standard.bool(forKey: "musicEnabled")
    @Published var bluetoothEnabled = UserDefaults.standard.bool(forKey: "bluetoothEnabled")
    @Published var captureClipboard = UserDefaults.standard.object(forKey: "captureClipboard") as? Bool ?? true
    @Published var connected: [String: String] = [:]
    @Published var loginEnabled = SMAppService.mainApp.status == .enabled
    @Published var updateBusy = false
    @Published var updateURL: URL?
    var thumbnails: [UUID: NSImage] = [:]
    var onLayout: (() -> Void)?
    var onPreview: ((ClipboardEntry) -> Void)?
    var onReset: (() -> Void)?
    var dialogOpen = false
    var locked: Bool {
        get { UserDefaults.standard.bool(forKey: "positionLocked") }
        set { UserDefaults.standard.set(newValue, forKey: "positionLocked"); objectWillChange.send() }
    }
    private let mediaService = MediaService()
    private var queue = NoticeQueue()
    private var timer: Timer?
    private var pasteboardCount = NSPasteboard.general.changeCount
    private var tickCount = 0
    private var lastTick = ProcessInfo.processInfo.systemUptime
    private var mediaKey = ""
    private var bluetoothBusy = false
    private var knownBluetooth: [String: String] = [:]
    private let folder: URL

    init() {
        folder = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("NovaLand", isDirectory: true)
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        todos = load("todos.json", fallback: [])
        reminders = load("reminders.json", fallback: [])
        let pinned: [ClipboardEntry] = load("pinned.json", fallback: [])
        clipboard = pinned.filter { $0.pinned }
        for entry in clipboard { if let png = entry.png { thumbnails[entry.id] = ClipboardImages.thumbnail(png) } }
        let minutes = UserDefaults.standard.integer(forKey: "focusMinutes")
        focus.set(minutes: minutes == 0 ? 25 : minutes)
    }
    func start() {
        if timer != nil { return }
        let timer = Timer(timeInterval: 1, repeats: true) { [weak self] _ in self?.tick() }
        timer.tolerance = 0.15; RunLoop.main.add(timer, forMode: .common); self.timer = timer
        let hour = Calendar.current.component(.hour, from: Date())
        let title = hour >= 5 && hour < 12 ? "Günaydın" : hour < 18 && hour >= 12 ? "İyi günler" : hour < 22 && hour >= 18 ? "İyi akşamlar" : "İyi geceler"
        push(key: "welcome", title: title, subtitle: "NovaLand yanında.", symbol: "sparkles", priority: 30)
    }
    private func tick() {
        now = Date(); tickCount += 1
        let uptime = ProcessInfo.processInfo.systemUptime
        queue.advance(seconds: uptime - lastTick, paused: expanded || dialogOpen); lastTick = uptime
        syncNotice()
        if focus.end != nil && focus.remaining(at: now) == 0 {
            focus.end = nil; focus.rest = 0
            push(key: "focus", title: "Mola zamanı", subtitle: "Biraz nefes al.", symbol: "cup.and.saucer.fill", priority: 60)
            NSSound(named: "Glass")?.play()
        }
        let due = reminders.filter { !$0.delivered && $0.due <= now }
        for item in due {
            if let index = reminders.firstIndex(where: { $0.id == item.id }) { reminders[index].delivered = true }
            push(key: item.id.uuidString, title: item.title, subtitle: item.due.formatted(date: .omitted, time: .shortened), symbol: "alarm.fill", priority: 100)
            NSSound(named: "Glass")?.play()
        }
        if !due.isEmpty { save(reminders, to: "reminders.json") }
        if captureClipboard { readClipboard() }
        if musicEnabled && tickCount % 3 == 0 { refreshMedia() }
        if bluetoothEnabled && tickCount % 5 == 0 { refreshBluetooth() }
    }
    func push(key: String, title: String, subtitle: String, symbol: String, priority: Int) {
        queue.push(IslandNotice(key: key, title: title, subtitle: subtitle, symbol: symbol, priority: priority)); syncNotice()
    }
    private func syncNotice() {
        let before = notice?.key
        notice = queue.current
        if before != notice?.key { onLayout?() }
    }
    func toggleTodo(_ id: UUID) {
        guard let index = todos.firstIndex(where: { $0.id == id }) else { return }
        todos[index].done.toggle(); save(todos, to: "todos.json")
    }
    func addTodo(_ text: String) {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty else { return }
        todos.append(TodoItem(title: String(value.prefix(1000)))); save(todos, to: "todos.json")
    }
    func deleteTodo(_ id: UUID) { todos.removeAll { $0.id == id }; save(todos, to: "todos.json") }
    func clearCompleted() { todos.removeAll(where: \.done); save(todos, to: "todos.json") }
    func addReminder(_ text: String, time: Date) {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty else { return }
        reminders.append(ReminderItem(title: String(value.prefix(500)), due: CoreRules.nextOccurrence(time: time, now: Date())))
        reminders.sort { $0.due < $1.due }; save(reminders, to: "reminders.json")
    }
    func deleteReminder(_ id: UUID) { reminders.removeAll { $0.id == id }; save(reminders, to: "reminders.json") }
    func setFocus(_ minutes: Int) {
        focus.set(minutes: minutes); UserDefaults.standard.set(focus.duration / 60, forKey: "focusMinutes")
    }
    func copy(_ entry: ClipboardEntry) {
        let pb = NSPasteboard.general; pb.clearContents()
        if let png = entry.png { pb.setData(png, forType: .png) }
        else if let text = entry.text { pb.setString(text, forType: .string) }
        pasteboardCount = pb.changeCount; message = "Kopyalandı."
    }
    func pin(_ id: UUID) {
        guard let index = clipboard.firstIndex(where: { $0.id == id }) else { return }
        clipboard[index].pinned.toggle(); clipboard = CoreRules.trim(clipboard); persistPins(); purgeThumbnails()
    }
    func clearRecent() { clipboard.removeAll { !$0.pinned }; purgeThumbnails() }
    private func purgeThumbnails() { let ids = Set(clipboard.map(\.id)); thumbnails = thumbnails.filter { ids.contains($0.key) } }
    private func persistPins() { save(clipboard.filter(\.pinned), to: "pinned.json") }
    private func readClipboard() {
        let pb = NSPasteboard.general
        guard pb.changeCount != pasteboardCount else { return }; pasteboardCount = pb.changeCount
        // Password managers can mark clipboard content confidential or transient.
        let excluded = ["org.nspasteboard.ConcealedType", "org.nspasteboard.TransientType", "org.nspasteboard.AutoGeneratedType"]
        guard !excluded.contains(where: { pb.availableType(from: [NSPasteboard.PasteboardType($0)]) != nil }) else { return }
        if let data = pb.data(forType: .png) ?? pb.data(forType: .tiff), let png = ClipboardImages.png(from: data) {
            guard !clipboard.contains(where: { $0.png == png }) else { return }
            let entry = ClipboardEntry(png: png); thumbnails[entry.id] = ClipboardImages.thumbnail(png)
            clipboard.insert(entry, at: 0)
        } else if let text = pb.string(forType: .string), !text.isEmpty, text.utf8.count <= 1_000_000 {
            guard !clipboard.contains(where: { $0.text == text }) else { return }
            clipboard.insert(ClipboardEntry(text: text), at: 0)
        } else { return }
        clipboard = CoreRules.trim(clipboard); purgeThumbnails()
    }
    func enableMusic(_ enabled: Bool) {
        musicEnabled = enabled; UserDefaults.standard.set(enabled, forKey: "musicEnabled")
        if enabled { refreshMedia() } else { media = MediaSnapshot(); mediaKey = "" }
    }
    func refreshMedia() {
        mediaService.refresh { [weak self] result in
            guard let self = self, self.musicEnabled else { return }
            guard let result = result else { self.media = MediaSnapshot(); return }
            if !self.mediaKey.isEmpty && self.mediaKey != result.key {
                self.push(key: "song", title: result.title, subtitle: result.artist, symbol: "music.note", priority: 20)
            }
            self.mediaKey = result.key; self.media = result
        }
    }
    func musicCommand(_ command: String) { mediaService.command(command, app: media.app) }
    func enableBluetooth(_ enabled: Bool) {
        bluetoothEnabled = enabled; UserDefaults.standard.set(enabled, forKey: "bluetoothEnabled")
        if enabled { refreshBluetooth() } else { connected = [:]; knownBluetooth = [:] }
    }
    private func refreshBluetooth() {
        guard !bluetoothBusy else { return }; bluetoothBusy = true
        DispatchQueue.global(qos: .utility).async {
            let devices = BluetoothService.connected()
            DispatchQueue.main.async {
                self.bluetoothBusy = false
                guard self.bluetoothEnabled else { return }
                for (id, name) in devices where self.knownBluetooth[id] == nil {
                    self.push(key: "bt:" + id, title: name, subtitle: "Bağlandı", symbol: "headphones", priority: 70)
                }
                for (id, name) in self.knownBluetooth where devices[id] == nil {
                    self.push(key: "bt:" + id, title: name, subtitle: "Bağlantı kesildi", symbol: "headphones", priority: 70)
                }
                self.knownBluetooth = devices; self.connected = devices
            }
        }
    }
    func setLogin(_ enabled: Bool) {
        do { if enabled { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() } }
        catch { message = "Otomatik başlangıç değiştirilemedi. Uygulamayı Applications klasörüne taşı ve sistem ayarlarını kontrol et." }
        loginEnabled = SMAppService.mainApp.status == .enabled
    }
    func checkUpdates() {
        guard !updateBusy else { return }; updateBusy = true; message = "Mac sürümleri kontrol ediliyor…"
        var request = URLRequest(url: URL(string: "https://api.github.com/repos/FatihSygl/NovaLand/releases?per_page=30")!)
        request.setValue("NovaLand-macOS/0.1.0", forHTTPHeaderField: "User-Agent"); request.timeoutInterval = 15
        URLSession.shared.dataTask(with: request) { data, response, error in
            var url: URL?, text = "Güncelleme kontrol edilemedi."
            if error == nil, let response = response as? HTTPURLResponse, response.statusCode == 200,
               let data = data, data.count < 2_000_000, let releases = try? JSONSerialization.jsonObject(with: data) as? [[String: Any]],
               let release = releases.first(where: { ($0["tag_name"] as? String)?.hasPrefix("mac-v") == true && ($0["draft"] as? Bool) != true }),
               let tag = release["tag_name"] as? String {
                let current = "mac-v" + (Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "0.1.0")
                if String(tag.dropFirst(5)).compare(String(current.dropFirst(5)), options: .numeric) == .orderedDescending {
                    url = URL(string: release["html_url"] as? String ?? ""); text = "Yeni Mac sürümü hazır. İndirme sayfasını açabilirsin."
                } else { text = "Güncel Mac sürümünü kullanıyorsun." }
            }
            DispatchQueue.main.async { self.updateBusy = false; self.updateURL = url; self.message = text }
        }.resume()
    }
    private func load<T: Decodable>(_ name: String, fallback: T) -> T {
        guard let data = try? Data(contentsOf: folder.appendingPathComponent(name)), let result = try? JSONDecoder().decode(T.self, from: data) else { return fallback }
        return result
    }
    private func save<T: Encodable>(_ value: T, to name: String) {
        do { try JSONEncoder().encode(value).write(to: folder.appendingPathComponent(name), options: .atomic) }
        catch { message = "Kaydedilemedi. Disk alanını ve klasör izinlerini kontrol et." }
    }
}

enum IslandTab: String, CaseIterable {
    case music, focus, todo, clipboard, reminders, settings
    var title: String { switch self { case .music: return "Müzik"; case .focus: return "Odak"; case .todo: return "Yapılacaklar"; case .clipboard: return "Pano"; case .reminders: return "Hatırlatıcılar"; case .settings: return "Ayarlar" } }
    var symbol: String { switch self { case .music: return "music.note"; case .focus: return "timer"; case .todo: return "checklist"; case .clipboard: return "doc.on.clipboard"; case .reminders: return "bell"; case .settings: return "gearshape" } }
}
