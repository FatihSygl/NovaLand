import SwiftUI
import AppKit

struct IslandView: View {
    @ObservedObject var model: IslandModel
    var topInset: CGFloat
    var hover: (Bool) -> Void
    var drag: (CGSize, Bool) -> Void
    @State private var todoText = ""
    @State private var reminderText = ""
    @State private var reminderTime = Date().addingTimeInterval(60)
    @State private var minutes = "25"
    private let accent = Color(red: 0.73, green: 0.96, blue: 0.52)
    var body: some View {
        VStack(spacing: 0) {
            if topInset > 0 { Color.clear.frame(height: topInset) }
            header.frame(height: 54)
                .contentShape(Rectangle())
                .onTapGesture { model.expanded.toggle(); if model.expanded { NSApp.activate(ignoringOtherApps: true) } }
                .simultaneousGesture(DragGesture(minimumDistance: 8).onChanged { if !model.locked { drag($0.translation, false) } }.onEnded { if !model.locked { drag($0.translation, true) } })
            if model.expanded {
                ScrollView {
                    VStack(alignment: .leading, spacing: 18) {
                        Text(model.tab.title).font(.system(size: 23, weight: .semibold)).padding(.bottom, 2)
                        content
                    }.frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal, 23).padding(.vertical, 18)
                }.frame(maxHeight: .infinity)
                HStack(spacing: 10) {
                    ForEach(IslandTab.allCases, id: \.self) { tab in
                        Button { model.tab = tab } label: {
                            Image(systemName: tab.symbol).font(.system(size: 18, weight: .medium))
                                .frame(width: 43, height: 40)
                                .background(model.tab == tab ? Color.white.opacity(0.12) : .clear, in: Capsule())
                        }.buttonStyle(.plain).foregroundColor(model.tab == tab ? .white : .gray).help(tab.title)
                    }
                    Spacer(minLength: 0)
                    Button { model.expanded = false } label: { Image(systemName: "chevron.up").frame(width: 24, height: 32) }.buttonStyle(.plain).help("Küçült")
                }.padding(.horizontal, 17).padding(.vertical, 12)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .foregroundColor(.white).background(Color(red: 0.035, green: 0.035, blue: 0.04))
        .clipShape(RoundedRectangle(cornerRadius: model.expanded ? 28 : 24, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: model.expanded ? 28 : 24).stroke(.white.opacity(0.07), lineWidth: 0.5))
        .onHover(perform: hover).preferredColorScheme(.dark).environment(\.locale, Locale(identifier: "tr_TR"))
    }
    private var header: some View {
        HStack(spacing: 11) {
            if let notice = model.notice {
                Image(systemName: notice.symbol).font(.system(size: 21)).foregroundColor(notice.priority >= 100 ? .orange : accent)
                VStack(alignment: .leading, spacing: 3) {
                    Text(notice.title).font(.system(size: 12, weight: .semibold)).lineLimit(1)
                    Text(notice.subtitle).font(.system(size: 10)).foregroundColor(.gray).lineLimit(1)
                }
                Spacer(minLength: 0)
                Image(systemName: "sparkle").foregroundColor(accent.opacity(0.8))
            } else if model.focus.end != nil {
                Image(systemName: "timer").foregroundColor(accent)
                Text(CoreRules.clock(model.focus.remaining(at: model.now))).font(.system(size: 18, weight: .medium, design: .rounded)).monospacedDigit()
                Spacer(minLength: 0)
                Image(systemName: model.media.playing ? "music.note" : "leaf").foregroundColor(accent)
            } else if !model.media.app.isEmpty {
                Image(systemName: "music.note").foregroundColor(accent)
                VStack(alignment: .leading, spacing: 3) {
                    Text(model.media.title).font(.system(size: 12, weight: .semibold)).lineLimit(1)
                    Text(model.media.artist).font(.system(size: 10)).foregroundColor(.gray).lineLimit(1)
                }
                Spacer(minLength: 0)
                Image(systemName: model.media.playing ? "play.fill" : "pause.fill").font(.system(size: 10)).foregroundColor(accent)
            } else {
                VStack(alignment: .leading, spacing: 2) {
                    Text(model.now, format: .dateTime.day().month(.abbreviated)).font(.system(size: 11, weight: .medium))
                    Text(model.now, format: .dateTime.weekday(.wide)).font(.system(size: 9)).foregroundColor(.gray)
                }
                Spacer()
                Text(model.now, style: .time).font(.system(size: 19, weight: .light)).monospacedDigit()
            }
            if model.expanded {
                Button { model.locked.toggle() } label: { Image(systemName: model.locked ? "lock.fill" : "lock.open").font(.system(size: 11)).frame(width: 24, height: 28) }.buttonStyle(.plain).help("Konumu kilitle")
                Button { model.onReset?() } label: { Image(systemName: "arrow.counterclockwise").font(.system(size: 11)).frame(width: 24, height: 28) }.buttonStyle(.plain).help("Konumu sıfırla")
            }
        }.padding(.horizontal, 20)
    }
    @ViewBuilder private var content: some View {
        switch model.tab {
        case .music: music
        case .focus: focus
        case .todo: todo
        case .clipboard: clipboard
        case .reminders: reminders
        case .settings: settings
        }
    }
    private var music: some View {
        VStack(spacing: 20) {
            ZStack {
                RoundedRectangle(cornerRadius: 24).fill(LinearGradient(colors: [accent.opacity(0.19), .white.opacity(0.035)], startPoint: .topLeading, endPoint: .bottomTrailing))
                Image(systemName: "music.note").font(.system(size: 46, weight: .light)).foregroundColor(accent)
            }.frame(width: 126, height: 126).padding(.top, 8)
            VStack(spacing: 6) {
                Text(model.media.title).font(.system(size: 18, weight: .semibold)).lineLimit(2).multilineTextAlignment(.center)
                Text(model.media.artist).font(.system(size: 12)).foregroundColor(.gray)
            }
            if !model.media.app.isEmpty {
                VStack(spacing: 8) {
                    ProgressView(value: min(max(0, model.media.position), max(1, model.media.duration)), total: max(1, model.media.duration)).tint(accent)
                    HStack {
                        Text(CoreRules.clock(Int(model.media.position)))
                        Spacer()
                        Text("−" + CoreRules.clock(Int(max(0, model.media.duration - model.media.position))))
                    }.font(.system(size: 10)).foregroundColor(.gray).monospacedDigit()
                }
                HStack(spacing: 28) {
                    transport("backward.end.fill", "previous track")
                    transport(model.media.playing ? "pause.fill" : "play.fill", "playpause")
                    transport("forward.end.fill", "next track")
                }
                Text(model.media.app).font(.system(size: 10)).foregroundColor(.gray)
            } else {
                Text("Önce Spotify veya Music’te bir şarkı aç.").font(.system(size: 11)).foregroundColor(.gray)
            }
            if !model.musicEnabled { Button("Spotify / Music bağla") { model.enableMusic(true) }.buttonStyle(.bordered) }
        }.frame(maxWidth: .infinity)
    }
    private func transport(_ icon: String, _ command: String) -> some View {
        Button { model.musicCommand(command) } label: { Image(systemName: icon).font(.system(size: 22)).frame(width: 52, height: 44) }.buttonStyle(.plain)
    }
    private var focus: some View {
        VStack(spacing: 23) {
            Text("KENDİNE BİR ALAN AÇ").font(.system(size: 9, weight: .medium)).tracking(2).foregroundColor(.gray)
            Text(CoreRules.clock(model.focus.remaining(at: model.now))).font(.system(size: 65, weight: .ultraLight, design: .rounded)).monospacedDigit().padding(.vertical, 10)
            HStack {
                Button(model.focus.end == nil ? "Başlat" : "Duraklat") { model.focus.toggle(at: Date()) }.buttonStyle(.borderedProminent).tint(accent).foregroundColor(.black)
                Button { model.focus.reset() } label: { Image(systemName: "arrow.counterclockwise") }.buttonStyle(.bordered).help("Sayacı sıfırla")
            }
            HStack(spacing: 10) {
                TextField("Dakika", text: $minutes).frame(width: 65).textFieldStyle(.roundedBorder).onSubmit { applyMinutes() }
                Text("dakika").font(.system(size: 11)).foregroundColor(.gray)
                Button("Uygula", action: applyMinutes).buttonStyle(.bordered)
            }
            HStack { ForEach([5,25,50], id: \.self) { value in Button("\(value) dk") { minutes = String(value); model.setFocus(value) }.buttonStyle(.bordered) } }
            Button("Hatırlatıcı ekle") { model.tab = .reminders }.buttonStyle(.plain).foregroundColor(.gray)
        }.frame(maxWidth: .infinity).onAppear { minutes = String(model.focus.duration / 60) }
    }
    private func applyMinutes() { if let value = Int(minutes), (1...1440).contains(value) { model.setFocus(value) } }
    private var todo: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                TextField("Yeni bir görev…", text: $todoText).textFieldStyle(.plain).onSubmit { addTodo() }
                Button(action: addTodo) { Image(systemName: "plus.circle.fill").font(.system(size: 20)).foregroundColor(accent) }.buttonStyle(.plain)
            }.padding(14).background(.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 14))
            if model.todos.isEmpty { empty("checklist", "Aklındakileri buraya bırak.") }
            ForEach(model.todos) { item in
                HStack(spacing: 10) {
                    Button { model.toggleTodo(item.id) } label: { Image(systemName: item.done ? "checkmark.circle.fill" : "circle").font(.system(size: 22)).frame(width: 32, height: 34).contentShape(Rectangle()) }.buttonStyle(.plain).foregroundColor(item.done ? accent : .gray)
                    Text(item.title).font(.system(size: 12)).strikethrough(item.done).foregroundColor(item.done ? .gray : .white).frame(maxWidth: .infinity, alignment: .leading)
                    Button { model.deleteTodo(item.id) } label: { Image(systemName: "xmark").font(.system(size: 10)).frame(width: 26, height: 30) }.buttonStyle(.plain).foregroundColor(.gray)
                }.padding(7).background(.white.opacity(0.045), in: RoundedRectangle(cornerRadius: 12))
            }
            if model.todos.contains(where: \.done) { Button("Tamamlananları temizle") { model.clearCompleted() }.buttonStyle(.plain).font(.system(size: 10)).foregroundColor(.gray) }
        }
    }
    private func addTodo() { model.addTodo(todoText); todoText = "" }
    private var clipboard: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack { Text("10 metin · 7 görsel · sabitlenenler ayrı").font(.system(size: 10)).foregroundColor(.gray); Spacer(); Button("Temizle") { model.clearRecent() }.buttonStyle(.plain).font(.system(size: 10)) }
            if model.clipboard.isEmpty { empty("doc.on.clipboard", "Kopyaladıkların burada görünecek.") }
            ForEach(model.clipboard.sorted { $0.pinned && !$1.pinned }) { entry in
                HStack(spacing: 11) {
                    if let thumb = model.thumbnails[entry.id] {
                        Button { model.onPreview?(entry) } label: { Image(nsImage: thumb).resizable().scaledToFit().frame(width: 74, height: 58).background(.white.opacity(0.03), in: RoundedRectangle(cornerRadius: 8)) }.buttonStyle(.plain).help("Görseli büyüt")
                    } else { Text(entry.text ?? "Görsel").font(.system(size: 11)).lineLimit(3).frame(maxWidth: .infinity, alignment: .leading) }
                    Spacer(minLength: 0)
                    Button { model.pin(entry.id) } label: { Image(systemName: entry.pinned ? "pin.fill" : "pin").frame(width: 25, height: 30) }.buttonStyle(.plain).foregroundColor(entry.pinned ? accent : .gray).help("Sabitle / ayır")
                    Button { model.copy(entry) } label: { Image(systemName: "doc.on.doc").frame(width: 25, height: 30) }.buttonStyle(.plain).help("Kopyala")
                }.padding(12).background(.white.opacity(0.055), in: RoundedRectangle(cornerRadius: 14))
            }
            if !model.captureClipboard { Text("Pano takibi Ayarlar’da kapalı.").font(.system(size: 11)).foregroundColor(.orange) }
        }
    }
    private var reminders: some View {
        VStack(alignment: .leading, spacing: 14) {
            VStack(alignment: .leading, spacing: 12) {
                TextField("Neyi hatırlatayım?", text: $reminderText).textFieldStyle(.plain).onSubmit { addReminder() }
                HStack { DatePicker("Saat", selection: $reminderTime, displayedComponents: .hourAndMinute).labelsHidden(); Spacer(); Button("Hatırlatıcı ekle", action: addReminder).buttonStyle(.bordered).disabled(reminderText.trimmingCharacters(in: .whitespaces).isEmpty) }
            }.padding(15).background(.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 15))
            Text("Geçen saat yarına kurulur. Uygulama açıkken çalışır; uyku sonrası gecikenler bildirilir.").font(.system(size: 10)).foregroundColor(.gray)
            ForEach(model.reminders) { item in
                HStack {
                    Image(systemName: item.delivered ? "checkmark.circle" : "alarm").foregroundColor(item.delivered ? .gray : accent)
                    VStack(alignment: .leading, spacing: 4) { Text(item.title).font(.system(size: 12)); Text(item.due.formatted(date: .abbreviated, time: .shortened)).font(.system(size: 10)).foregroundColor(.gray) }
                    Spacer()
                    Button { model.deleteReminder(item.id) } label: { Image(systemName: "xmark").frame(width: 27, height: 32) }.buttonStyle(.plain).foregroundColor(.gray)
                }.padding(12).background(.white.opacity(0.045), in: RoundedRectangle(cornerRadius: 13))
            }
        }
    }
    private func addReminder() { model.addReminder(reminderText, time: reminderTime); reminderText = "" }
    private var settings: some View {
        VStack(alignment: .leading, spacing: 17) {
            HStack(spacing: 12) {
                if let image = NSImage(named: "Logo") { Image(nsImage: image).resizable().scaledToFit().frame(width: 32, height: 32) }
                VStack(alignment: .leading, spacing: 4) { Text("NovaLand").font(.system(size: 17, weight: .semibold)); Text("macOS · 0.1.0 önizleme").font(.system(size: 10)).foregroundColor(.gray) }
            }
            VStack(spacing: 16) {
                Toggle("Pano geçmişini tut", isOn: Binding(get: { model.captureClipboard }, set: { model.captureClipboard = $0; UserDefaults.standard.set($0, forKey: "captureClipboard") }))
                Toggle("Spotify / Music bağlantısı", isOn: Binding(get: { model.musicEnabled }, set: { model.enableMusic($0) }))
                Toggle("Bluetooth bildirimleri", isOn: Binding(get: { model.bluetoothEnabled }, set: { model.enableBluetooth($0) }))
                Toggle("Mac açılınca başlat", isOn: Binding(get: { model.loginEnabled }, set: { model.setLogin($0) }))
            }.font(.system(size: 11)).toggleStyle(.switch).tint(accent).padding(15).background(.white.opacity(0.05), in: RoundedRectangle(cornerRadius: 16))
            if model.bluetoothEnabled {
                ForEach(model.connected.keys.sorted(), id: \.self) { key in Label(model.connected[key] ?? "Cihaz", systemImage: "headphones").font(.system(size: 11)) }
                Text("Bu önizlemede pil yüzdesi gösterilmiyor.").font(.system(size: 10)).foregroundColor(.gray)
            }
            Text("Görevler, hatırlatıcılar ve sabitlediklerin bu Mac’te saklanır. Diğer pano geçmişi oturumla sınırlıdır.").font(.system(size: 10)).foregroundColor(.gray)
            HStack {
                Button("Güncellemeleri denetle") { model.checkUpdates() }.buttonStyle(.bordered).disabled(model.updateBusy)
                if let url = model.updateURL { Link("Yeni sürümü indir ↗", destination: url).font(.system(size: 10)) }
            }
            Text(model.message).font(.system(size: 10)).foregroundColor(.gray).fixedSize(horizontal: false, vertical: true)
            Button("Uygulamadan çık") { NSApp.terminate(nil) }.buttonStyle(.plain).font(.system(size: 11)).foregroundColor(.red.opacity(0.8))
        }
    }
    private func empty(_ symbol: String, _ text: String) -> some View {
        VStack(spacing: 14) { Image(systemName: symbol).font(.system(size: 30, weight: .ultraLight)); Text(text).font(.system(size: 12)) }.foregroundColor(.gray).frame(maxWidth: .infinity).padding(.vertical, 45)
    }
}
