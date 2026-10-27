import AppKit
import SwiftUI

final class IslandPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private let model = IslandModel()
    private var panel: IslandPanel!
    private var statusItem: NSStatusItem!
    private var preview: NSWindow?
    private var hoverWork: DispatchWorkItem?
    private var hideWork: DispatchWorkItem?
    private var centerX: CGFloat = 0
    private var topY: CGFloat = 0
    private var hidden = false
    private var hovered = false
    private var dragOrigin: CGPoint?
    private var screen: NSScreen { NSScreen.screens.first(where: { NSPointInRect(NSPoint(x: centerX, y: topY - 1), $0.frame) }) ?? NSScreen.main ?? NSScreen.screens[0] }
    private var isDocked: Bool { abs(topY - screen.frame.maxY) < 2 }
    private var inset: CGFloat { isDocked ? screen.safeAreaInsets.top : 0 }
    func applicationDidFinishLaunching(_ notification: Notification) {
        let existing = NSRunningApplication.runningApplications(withBundleIdentifier: Bundle.main.bundleIdentifier ?? "dev.fatihsygl.NovaLand").filter { $0.processIdentifier != ProcessInfo.processInfo.processIdentifier }
        if !existing.isEmpty { existing[0].activate(options: [.activateIgnoringOtherApps]); NSApp.terminate(nil); return }
        NSApp.setActivationPolicy(.accessory)
        let main = NSScreen.main ?? NSScreen.screens[0]
        centerX = main.frame.midX; topY = main.frame.maxY
        if UserDefaults.standard.bool(forKey: "hasPosition") {
            let saved = NSPoint(x: UserDefaults.standard.double(forKey: "centerX"), y: UserDefaults.standard.double(forKey: "topY"))
            if NSScreen.screens.contains(where: { $0.frame.insetBy(dx: -1, dy: -1).contains(NSPoint(x: saved.x, y: saved.y - 1)) }) { centerX = saved.x; topY = saved.y }
        }
        panel = IslandPanel(contentRect: .zero, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.isOpaque = false; panel.backgroundColor = .clear; panel.hasShadow = true
        panel.level = NSWindow.Level(rawValue: NSWindow.Level.statusBar.rawValue + 1)
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.hidesOnDeactivate = false; panel.isReleasedWhenClosed = false
        panel.acceptsMouseMovedEvents = true; panel.delegate = self
        panel.contentView = NSHostingView(rootView: view())
        model.onLayout = { [weak self] in self?.layout() }
        model.onPreview = { [weak self] in self?.showPreview($0) }
        model.onReset = { [weak self] in self?.resetPosition() }
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let icon = NSImage(named: "Logo") { icon.size = NSSize(width: 19, height: 19); statusItem.button?.image = icon }
        else { statusItem.button?.image = NSImage(systemSymbolName: "capsule.fill", accessibilityDescription: "NovaLand") }
        statusItem.button?.toolTip = "NovaLand"
        let menu = NSMenu()
        menu.addItem(withTitle: "NovaLand’i aç", action: #selector(showExpanded), keyEquivalent: "")
        menu.addItem(withTitle: "Konumu sıfırla", action: #selector(resetPosition), keyEquivalent: "")
        menu.addItem(.separator()); menu.addItem(withTitle: "Çık", action: #selector(quit), keyEquivalent: "q")
        for item in menu.items { item.target = self }; statusItem.menu = menu
        NotificationCenter.default.addObserver(self, selector: #selector(screenChanged), name: NSApplication.didChangeScreenParametersNotification, object: nil)
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(wake), name: NSWorkspace.didWakeNotification, object: nil)
        layout(animated: false); panel.orderFrontRegardless(); model.start()
    }
    private func view() -> IslandView { IslandView(model: model, topInset: inset, hover: { [weak self] in self?.hover($0) }, drag: { [weak self] delta, ended in self?.drag(delta, ended: ended) }) }
    private func layout(animated: Bool = true) {
        guard panel != nil else { return }
        if model.expanded || model.notice != nil || !isDocked { hidden = false }
        let width: CGFloat = model.expanded ? 440 : model.notice != nil || !model.media.app.isEmpty ? 334 : 244
        let height: CGFloat = model.expanded ? min(570 + inset, screen.frame.height - 60) : 54 + inset
        let x = min(screen.frame.maxX - width, max(screen.frame.minX, centerX - width / 2))
        let y = hidden && isDocked ? screen.frame.maxY - 12 : max(screen.visibleFrame.minY, topY - height)
        panel.contentView = panel.contentView ?? NSHostingView(rootView: view())
        // Update the safe-area inset after docking or a screen change.
        if let host = panel.contentView as? NSHostingView<IslandView> { host.rootView = view() }
        let frame = NSRect(x: x, y: y, width: width, height: height)
        if animated { NSAnimationContext.runAnimationGroup { context in context.duration = 0.22; panel.animator().setFrame(frame, display: true) } }
        else { panel.setFrame(frame, display: true) }
        if model.expanded { panel.makeKeyAndOrderFront(nil) }
        if !hidden && !model.expanded && model.notice == nil && !hovered && !model.dialogOpen { scheduleHide() }
    }
    private func hover(_ inside: Bool) {
        hovered = inside; hoverWork?.cancel(); hideWork?.cancel()
        if inside {
            let work = DispatchWorkItem { [weak self] in guard let self = self, self.hovered else { return }; self.hidden = false; self.layout() }
            hoverWork = work; DispatchQueue.main.asyncAfter(deadline: .now() + 0.15, execute: work)
        } else { scheduleHide() }
    }
    private func scheduleHide() {
        hideWork?.cancel()
        let work = DispatchWorkItem { [weak self] in
            guard let self = self, !self.hovered, !self.model.expanded, self.model.notice == nil, !self.model.dialogOpen, self.isDocked else { return }
            self.hidden = true; self.layout()
        }
        hideWork = work; DispatchQueue.main.asyncAfter(deadline: .now() + 0.18, execute: work)
    }
    private func drag(_ delta: CGSize, ended: Bool) {
        hideWork?.cancel(); hoverWork?.cancel(); hidden = false
        if dragOrigin == nil { dragOrigin = CGPoint(x: centerX, y: topY) }
        centerX = dragOrigin!.x + delta.width; topY = dragOrigin!.y - delta.height
        if ended {
            let s = screen
            centerX = min(s.frame.maxX - 130, max(s.frame.minX + 130, centerX))
            topY = min(s.frame.maxY, max(s.visibleFrame.minY + 80, topY))
            if abs(s.frame.maxY - topY) < 28 { topY = s.frame.maxY }
            dragOrigin = nil
            UserDefaults.standard.set(true, forKey: "hasPosition")
            UserDefaults.standard.set(centerX, forKey: "centerX"); UserDefaults.standard.set(topY, forKey: "topY")
        }
        layout(animated: false)
    }
    @objc private func resetPosition() {
        let s = NSScreen.main ?? NSScreen.screens[0]; centerX = s.frame.midX; topY = s.frame.maxY
        hidden = false; UserDefaults.standard.removeObject(forKey: "hasPosition"); layout()
    }
    @objc private func showExpanded() { model.expanded = true; hidden = false; NSApp.activate(ignoringOtherApps: true); layout() }
    @objc private func screenChanged() { resetPosition() }
    @objc private func wake() { layout() }
    @objc private func quit() { NSApp.terminate(nil) }
    func windowDidResignKey(_ notification: Notification) { if (notification.object as? NSWindow) === panel && !model.dialogOpen { model.expanded = false } }
    func windowWillClose(_ notification: Notification) {
        if (notification.object as? NSWindow) === preview { preview = nil; model.dialogOpen = false; scheduleHide() }
    }
    private func showPreview(_ entry: ClipboardEntry) {
        guard let png = entry.png, let image = NSImage(data: png) else { return }
        preview?.close(); model.dialogOpen = true
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: min(860, screen.visibleFrame.width - 40), height: min(620, screen.visibleFrame.height - 40)), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        window.title = "Görsel önizlemesi"; window.backgroundColor = NSColor(white: 0.06, alpha: 1)
        window.level = panel.level; window.delegate = self; window.isReleasedWhenClosed = false
        window.contentView = NSHostingView(rootView: VStack(spacing: 12) {
            HStack { Text("Orijinal görsel").foregroundColor(.gray); Spacer(); Button("Kopyala") { self.model.copy(entry) } }.padding(15)
            Image(nsImage: image).resizable().scaledToFit().frame(maxWidth: .infinity, maxHeight: .infinity).padding(15)
        }.preferredColorScheme(.dark))
        preview = window; window.center(); NSApp.activate(ignoringOtherApps: true); window.makeKeyAndOrderFront(nil)
    }
}
let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()
