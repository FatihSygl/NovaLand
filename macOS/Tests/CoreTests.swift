import Foundation
@main struct CoreTests {
    static func main() throws {
        var checks = 0
        func expect(_ ok: Bool, _ label: String) { precondition(ok, label); checks += 1; print("PASS \(label)") }
        var queue = NoticeQueue()
        queue.push(IslandNotice(key: "song", title: "Song", subtitle: "", symbol: "", priority: 20))
        queue.push(IslandNotice(key: "headphone", title: "Connected", subtitle: "", symbol: "", priority: 70))
        expect(queue.current?.key == "headphone", "Bluetooth preempts song")
        queue.advance(seconds: 4, paused: true)
        expect(queue.current?.seconds == 8, "expanded panel pauses countdown")
        queue.advance(seconds: 8, paused: false)
        expect(queue.current?.key == "song", "song resumes after Bluetooth")
        queue.push(IslandNotice(key: "song", title: "New song", subtitle: "", symbol: "", priority: 20))
        expect(queue.current?.title == "New song", "song coalescing")
        queue.push(IslandNotice(key: "reminder", title: "Alarm", subtitle: "", symbol: "", priority: 100))
        expect(queue.current?.key == "reminder", "reminder has priority")
        queue.advance(seconds: 8, paused: false); queue.advance(seconds: 8, paused: false)
        expect(queue.current == nil, "queue drains")
        let now = Date(timeIntervalSince1970: 100000)
        var clock = FocusClock(); clock.set(minutes: 3); clock.toggle(at: now)
        expect(clock.remaining(at: now.addingTimeInterval(61)) == 119, "focus countdown uses deadline")
        clock.toggle(at: now.addingTimeInterval(61))
        expect(clock.end == nil && clock.rest == 119, "focus pause")
        clock.toggle(at: now.addingTimeInterval(120))
        expect(clock.remaining(at: now.addingTimeInterval(1000)) == 0, "sleep catch-up clamps zero")
        clock.set(minutes: 2000); expect(clock.duration == 86400, "custom minutes bounded")
        clock.reset(); expect(clock.rest == clock.duration, "focus reset")
        let pinned = ClipboardEntry(text: "pin", pinned: true)
        var entries = [pinned] + (0..<15).map { ClipboardEntry(text: String($0)) }
        entries += (0..<9).map { ClipboardEntry(png: Data([UInt8($0)])) }
        let trimmed = CoreRules.trim(entries)
        expect(trimmed.filter { !$0.pinned && !$0.isImage }.count == 10, "10 recent texts")
        expect(trimmed.filter { !$0.pinned && $0.isImage }.count == 7, "7 recent images")
        expect(trimmed.contains { $0.id == pinned.id }, "pins excluded from recent count")
        let restored = try JSONDecoder().decode([ClipboardEntry].self, from: JSONEncoder().encode(trimmed))
        expect(restored.count == trimmed.count && restored[0].pinned, "pinned persistence roundtrip")
        var calendar = Calendar(identifier: .gregorian); calendar.timeZone = TimeZone(secondsFromGMT: 0)!
        let morning = calendar.date(from: DateComponents(year: 2026, month: 10, day: 27, hour: 10))!
        let past = morning.addingTimeInterval(-3600)
        let next = CoreRules.nextOccurrence(time: past, now: morning, calendar: calendar)
        expect(calendar.component(.day, from: next) == 28, "past reminder goes to tomorrow")
        expect(CoreRules.clock(125) == "02:05", "countdown format")
        print("\(checks) core checks passed")
    }
}
