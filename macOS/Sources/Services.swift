import AppKit
import ImageIO
import IOBluetooth

struct MediaSnapshot: Equatable {
    var app = ""
    var title = "Müzik hazır olduğunda"
    var artist = "Spotify veya Music ile bağlan"
    var playing = false
    var duration: Double = 0
    var position: Double = 0
    var key: String { app + ":" + title + ":" + artist }
}
final class MediaService {
    private let worker = DispatchQueue(label: "NovaLand.media", qos: .utility)
    private var busy = false
    // No shell interpolation or private MediaRemote APIs. Scripts address running apps only.
    private func script(_ source: String) -> String? {
        let process = Process(), pipe = Pipe()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/osascript")
        process.arguments = ["-e", source]
        process.standardOutput = pipe; process.standardError = FileHandle.nullDevice
        do { try process.run() } catch { return nil }
        let deadline = Date().addingTimeInterval(2)
        while process.isRunning && Date() < deadline { Thread.sleep(forTimeInterval: 0.025) }
        if process.isRunning { process.terminate(); return nil }
        guard process.terminationStatus == 0 else { return nil }
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        guard data.count < 16384 else { return nil }
        return String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines)
    }
    func refresh(completion: @escaping (MediaSnapshot?) -> Void) {
        guard !busy else { return }; busy = true
        let running = NSWorkspace.shared.runningApplications.compactMap(\.bundleIdentifier)
        let apps = [("com.spotify.client", "Spotify"), ("com.apple.Music", "Music")].filter { running.contains($0.0) }
        worker.async {
            var snapshots: [MediaSnapshot] = []
            for (_, app) in apps {
                let duration = app == "Spotify" ? "(duration of current track) / 1000" : "duration of current track"
                let source = """
                tell application "\(app)"
                    if not (exists current track) then return ""
                    return (name of current track as text) & linefeed & (artist of current track as text) & linefeed & (player state as text) & linefeed & (\(duration) as text) & linefeed & (player position as text)
                end tell
                """
                guard let output = self.script(source) else { continue }
                let fields = output.components(separatedBy: "\n")
                guard fields.count >= 5 else { continue }
                snapshots.append(MediaSnapshot(app: app, title: fields[0], artist: fields[1], playing: fields[2] == "playing", duration: Double(fields[3].replacingOccurrences(of: ",", with: ".")) ?? 0, position: Double(fields[4].replacingOccurrences(of: ",", with: ".")) ?? 0))
            }
            let result = snapshots.first(where: \.playing) ?? snapshots.first
            DispatchQueue.main.async { self.busy = false; completion(result) }
        }
    }
    func command(_ command: String, app: String) {
        guard ["Spotify", "Music"].contains(app), ["playpause", "next track", "previous track"].contains(command) else { return }
        worker.async { _ = self.script("tell application \"\(app)\" to \(command)") }
    }
}

enum ClipboardImages {
    static func png(from data: Data) -> Data? {
        guard data.count <= 32 * 1024 * 1024,
              let source = CGImageSourceCreateWithData(data as CFData, nil),
              let props = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = props[kCGImagePropertyPixelWidth] as? Int,
              let height = props[kCGImagePropertyPixelHeight] as? Int,
              width > 0, height > 0, Double(width) * Double(height) <= 20_000_000,
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { return nil }
        return NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:])
    }
    static func thumbnail(_ data: Data) -> NSImage? {
        guard let source = CGImageSourceCreateWithData(data as CFData, nil),
              let image = CGImageSourceCreateThumbnailAtIndex(source, 0, [kCGImageSourceCreateThumbnailFromImageAlways: true, kCGImageSourceThumbnailMaxPixelSize: 240, kCGImageSourceCreateThumbnailWithTransform: true] as CFDictionary) else { return nil }
        return NSImage(cgImage: image, size: .zero)
    }
}

enum BluetoothService {
    // Optional, off by default. Read connections; do not initiate an inquiry or pairing.
    static func connected() -> [String: String] {
        var result: [String: String] = [:]
        for device in (IOBluetoothDevice.pairedDevices() as? [IOBluetoothDevice]) ?? [] where device.isConnected() {
            result[device.addressString ?? "unknown"] = device.name ?? "Bluetooth cihazı"
        }
        return result
    }
}
