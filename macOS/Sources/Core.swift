import Foundation

struct TodoItem: Codable, Identifiable, Equatable {
    var id = UUID()
    var title: String
    var done = false
}
struct ReminderItem: Codable, Identifiable, Equatable {
    var id = UUID()
    var title: String
    var due: Date
    var delivered = false
}
struct ClipboardEntry: Codable, Identifiable {
    var id = UUID()
    var text: String?
    var png: Data?
    var pinned = false
    var isImage: Bool { png != nil }
}
struct IslandNotice: Equatable {
    var key: String
    var title: String
    var subtitle: String
    var symbol: String
    var priority: Int
    var seconds: Double = 8
}
struct NoticeQueue {
    private(set) var current: IslandNotice?
    private var pending: [IslandNotice] = []
    mutating func push(_ notice: IslandNotice) {
        pending.removeAll { $0.key == notice.key }
        if current?.key == notice.key { current = notice; return }
        guard let active = current else { current = notice; return }
        if notice.priority > active.priority { pending.append(active); current = notice }
        else { pending.append(notice) }
        pending.sort { $0.priority > $1.priority }
    }
    mutating func advance(seconds: Double, paused: Bool) {
        guard !paused, var active = current else { return }
        active.seconds -= max(0, seconds)
        if active.seconds <= 0 { current = pending.isEmpty ? nil : pending.removeFirst() }
        else { current = active }
    }
}
struct FocusClock {
    var duration = 25 * 60
    var rest = 25 * 60
    var end: Date?
    func remaining(at now: Date) -> Int { end.map { max(0, Int(ceil($0.timeIntervalSince(now)))) } ?? rest }
    mutating func toggle(at now: Date) {
        if end != nil { rest = remaining(at: now); end = nil }
        else { if rest == 0 { rest = duration }; end = now.addingTimeInterval(Double(rest)) }
    }
    mutating func set(minutes: Int) { duration = min(1440, max(1, minutes)) * 60; reset() }
    mutating func reset() { end = nil; rest = duration }
}
enum CoreRules {
    static func trim(_ entries: [ClipboardEntry]) -> [ClipboardEntry] {
        var texts = 0, images = 0
        return entries.filter {
            if $0.pinned { return true }
            if $0.isImage { images += 1; return images <= 7 }
            texts += 1; return texts <= 10
        }
    }
    static func nextOccurrence(time: Date, now: Date, calendar: Calendar = .current) -> Date {
        let parts = calendar.dateComponents([.hour, .minute], from: time)
        return calendar.nextDate(after: now, matching: parts, matchingPolicy: .nextTime) ?? now.addingTimeInterval(60)
    }
    static func clock(_ seconds: Int) -> String { String(format: "%02d:%02d", seconds / 60, seconds % 60) }
}
