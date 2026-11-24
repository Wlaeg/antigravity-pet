import AppKit
import Carbon
import Darwin
import Foundation

// MARK: - Models & Config

struct PetConfig: Codable {
    var Enabled: Bool = true
    var Skin: String = "cyber-fox"
    var Left: Double = -1
    var Top: Double = -1
    var Topmost: Bool = true
    var BubbleMode: String = "always"
    var BubbleStyle: String = "simple"
    var PinnedConversationId: String = ""
    var Scale: Double = 1.0
    var AutoWalk: Bool = true
    var EyeFollow: Bool = true
    var AutoStartOnBoot: Bool = true
    var SoundAlert: Bool = false
    var Language: String = "auto"
    var AgyDetectedLocale: String = ""
    var CompletedTasksCount: Int = 0
    var ToolCallsCount: Int = 0
}

struct PetPack {
    let id: String
    let displayName: String
    let description: String
    let version: Int
    let spritesheetPath: String
    var image: NSImage?
}

struct ConvState {
    let conversationId: String
    var state: String
    var message: String
    var title: String
    var updatedAt: Date
}

enum PetAnimRow: Int {
    case idle = 0
    case walkRight = 1
    case walkLeft = 2
    case wave = 3
    case jump = 4
    case blocked = 5
    case needsInput = 6
    case running = 7
    case ready = 8
    case look0 = 9
    case look1 = 10
}

// MARK: - Localization

final class I18n {
    static let enPetMeta: [String: (String, String)] = [
        "cyber-fox": ("CyberFox", "Cyber blue fox companion (Default)"),
        "astro-bot": ("AstroBot", "Mini astronaut with glowing visor"),
        "shadow-cat": ("ShadowCat", "Golden-eyed nimble black cat"),
        "byte-ghost": ("ByteGhost", "Cute floating neon ghost"),
        "shiba-dog": ("ShibaDog", "Warm fluffy Shiba Inu"),
        "pixel-dragon": ("PixelDragon", "Emerald mini dragon with golden horns"),
        "android-bugdroid": ("Android Bugdroid", "Classic Google Android green robot mascot"),
        "flutter-dash": ("Flutter Dash", "Official Google Flutter & Dart blue bird mascot")
    ]

    static func detectAgyLocale() -> String {
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
        let agyDir = appSupport?.appendingPathComponent("Antigravity")
        if let sp = agyDir?.appendingPathComponent("app_storage.json"),
           let raw = try? String(contentsOf: sp, encoding: .utf8) {
            if raw.range(of: "\"(?:locale|language)\"\\s*:\\s*\"zh", options: .regularExpression) != nil {
                return "zh"
            }
            if raw.range(of: "\"(?:locale|language)\"\\s*:\\s*\"en", options: .regularExpression) != nil {
                return "en"
            }
        }
        if let lp = agyDir?.appendingPathComponent("localization-prefs.json"),
           let raw = try? String(contentsOf: lp, encoding: .utf8) {
            let low = raw.lowercased()
            if low.contains("\"usetw\":true") || low.contains("chinese") { return "zh" }
            if low.contains("english") { return "en" }
        }
        if let first = Locale.preferredLanguages.first?.lowercased() {
            if first.hasPrefix("zh") { return "zh" }
            if first.hasPrefix("en") { return "en" }
        }
        return "en"
    }

    static func effectiveLanguage(config: PetConfig) -> String {
        let mode = config.Language.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        if mode == "en" || mode.hasPrefix("en-") { return "en" }
        if mode == "zh" || mode.hasPrefix("zh-") { return "zh" }
        let agy = config.AgyDetectedLocale.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        if agy.hasPrefix("en") { return "en" }
        if agy.hasPrefix("zh") { return "zh" }
        let detected = detectAgyLocale()
        return detected.hasPrefix("zh") ? "zh" : "en"
    }

    static func translateBubble(state: String, rawMessage: String, lang: String) -> String {
        if lang != "en" {
            if !rawMessage.isEmpty { return rawMessage }
            switch state {
            case "needs_input": return "需要你确认!"
            case "blocked": return "任务出错 · 点击查看"
            case "ready": return "任务完成!"
            case "running": return "思考执行中..."
            default: return ""
            }
        }
        let msg = rawMessage.trimmingCharacters(in: .whitespacesAndNewlines)
        if msg.isEmpty {
            switch state {
            case "needs_input": return "Needs your input!"
            case "blocked": return "Blocked · Click to inspect"
            case "ready": return "Task complete!"
            case "running": return "Thinking..."
            default: return ""
            }
        }
        let map: [String: String] = [
            "思考规划中...": "Thinking...",
            "思考执行中...": "Working...",
            "处理结果中...": "Processing...",
            "任务完成!": "Task complete!",
            "任务出错 · 点击查看": "Task error · Click to view",
            "工具出错 · 点击查看": "Tool error · Click to view",
            "查看屏幕中...": "Viewing screen...",
            "操作桌面应用中...": "Controlling app...",
            "操控桌面中...": "Controlling desktop...",
            "编辑代码中...": "Editing code...",
            "检索代码中...": "Searching code...",
            "运行检查中...": "Running checks...",
            "快速分析代码中...": "Analyzing code...",
            "分析长文本中...": "Analyzing context...",
            "更新桌宠设置...": "Updating pet...",
            "调用扩展工具中...": "Running tool...",
            "执行命令中...": "Running command...",
            "读取文件中...": "Reading files...",
            "编写文件中...": "Writing files...",
            "修改代码中...": "Editing files...",
            "搜索资料中...": "Searching web...",
            "调度子代理中...": "Running subagent...",
            "等待你回答...": "Waiting for input...",
            "管理后台任务...": "Managing tasks...",
            "准备开始执行...": "Starting execution...",
            "执行任务中...": "Working..."
        ]
        if let mapped = map[msg] { return mapped }
        return msg
    }
}

// MARK: - Sprite Canvas View

final class PetCanvasView: NSView {
    weak var controller: PetController?

    override var isFlipped: Bool { true }

    override func draw(_ dirtyRect: NSRect) {
        guard let ctrl = controller, let ctx = NSGraphicsContext.current?.cgContext else { return }
        ctx.clear(bounds)

        let scale = CGFloat(ctrl.config.Scale)
        let spriteW = 140.0 * scale
        let spriteH = 152.0 * scale
        let spriteX = (bounds.width - spriteW) / 2.0
        let spriteY = bounds.height - spriteH - 12.0

        // 状态光环
        let effState = ctrl.currentEffectiveState()
        if effState != "idle" {
            let auraColor: NSColor
            switch effState {
            case "needs_input": auraColor = NSColor(calibratedRed: 1.0, green: 0.70, blue: 0.0, alpha: 0.28)
            case "blocked": auraColor = NSColor(calibratedRed: 1.0, green: 0.35, blue: 0.37, alpha: 0.28)
            case "ready": auraColor = NSColor(calibratedRed: 0.30, green: 0.85, blue: 0.39, alpha: 0.28)
            default: auraColor = NSColor(calibratedRed: 0.31, green: 0.76, blue: 0.97, alpha: 0.25)
            }
            let auraRect = NSRect(x: spriteX + spriteW * 0.12, y: bounds.height - 26.0, width: spriteW * 0.76, height: 18.0)
            ctx.saveGState()
            ctx.setFillColor(auraColor.cgColor)
            ctx.fillEllipse(in: auraRect)
            ctx.restoreGState()
        }

        // 绘制精灵图（严格最近邻无插值）
        if let pack = ctrl.activePack(), let nsImg = pack.image,
           let cgImg = nsImg.cgImage(forProposedRect: nil, context: nil, hints: nil) {
            let (row, frame) = ctrl.currentRowAndFrame()
            let cellW = 192
            let cellH = 208
            let cropRect = CGRect(x: frame * cellW, y: row * cellH, width: cellW, height: cellH)
            if let cropped = cgImg.cropping(to: cropRect) {
                ctx.saveGState()
                ctx.interpolationQuality = .none
                let destRect = NSRect(x: spriteX, y: spriteY, width: spriteW, height: spriteH)
                let pivotX = destRect.midX
                let pivotY = destRect.minY + destRect.height * 0.22
                let angleRad = ctrl.dragCurrentAngle * .pi / 180.0
                ctx.translateBy(x: pivotX, y: pivotY)
                ctx.rotate(by: angleRad)
                ctx.scaleBy(x: ctrl.dragScaleX, y: ctrl.dragScaleY)
                ctx.translateBy(x: -pivotX, y: -pivotY)
                // CoreGraphics draw(in:) 在 flipped NSView 上会倒置，手动翻转 Y
                ctx.translateBy(x: destRect.minX, y: destRect.maxY)
                ctx.scaleBy(x: 1.0, y: -1.0)
                ctx.draw(cropped, in: CGRect(x: 0, y: 0, width: destRect.width, height: destRect.height))
                ctx.restoreGState()
            }
        }

        // 绘制头顶气泡（支持简易模式 & 详细堆叠展开模式）
        bubbleHitRects.removeAll()
        if ctrl.config.BubbleMode == "off" { return }

        if ctrl.isDetailedStyle() {
            let tasks = ctrl.orderedVisibleTasks()
            if tasks.isEmpty { return }
            let font = NSFont.systemFont(ofSize: 11.5, weight: .bold)

            if ctrl.bubbleExpanded {
                let showCount = min(5, tasks.count)
                var curBottom = spriteY - 10.0
                for i in 0..<showCount {
                    let t = tasks[i]
                    let isPinned = !ctrl.config.PinnedConversationId.isEmpty && t.conversationId == ctrl.config.PinnedConversationId
                    let label = (isPinned ? "📌 " : "") + ctrl.formatDetailedTaskLabel(t)
                    let accent = isPinned ? NSColor.systemYellow : ctrl.accentColor(for: t.state)
                    let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: NSColor.white]
                    let attrStr = NSAttributedString(string: label, attributes: attrs)
                    let textSize = attrStr.size()
                    let padX: CGFloat = 12.0
                    let bh: CGFloat = 26.0
                    let bw = min(bounds.width - 10.0, max(96.0, textSize.width + padX * 2.0 + 12.0))
                    let bx = (bounds.width - bw) / 2.0
                    let by = curBottom - bh
                    if by < 2.0 { break }

                    let rect = NSRect(x: bx, y: by, width: bw, height: bh)
                    let path = NSBezierPath(roundedRect: rect, xRadius: 9.0, yRadius: 9.0)
                    NSColor(calibratedRed: 0.08, green: 0.10, blue: 0.16, alpha: 0.95).setFill()
                    path.fill()
                    accent.setStroke()
                    path.lineWidth = isPinned ? 1.8 : 1.3
                    path.stroke()

                    // 状态圆点
                    let dotRect = NSRect(x: bx + 9.0, y: by + (bh - 7.0) / 2.0, width: 7.0, height: 7.0)
                    accent.setFill()
                    NSBezierPath(ovalIn: dotRect).fill()

                    let tx = bx + 21.0
                    let ty = by + (bh - textSize.height) / 2.0
                    attrStr.draw(in: NSRect(x: tx, y: ty, width: bw - 28.0, height: textSize.height))
                    bubbleHitRects.append((rect, t.conversationId))
                    curBottom = by - 5.0
                }
                return
            }

            // 详细模式收起态（多会话时显示堆叠阴影背板 + +N 角标）
            let top = tasks[0]
            let isPinned = !ctrl.config.PinnedConversationId.isEmpty && top.conversationId == ctrl.config.PinnedConversationId
            var label = (isPinned ? "📌 " : "") + ctrl.formatDetailedTaskLabel(top)
            if tasks.count > 1 { label += " (+\(tasks.count - 1))" }
            let accent = isPinned ? NSColor.systemYellow : ctrl.accentColor(for: top.state)
            let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: NSColor.white]
            let attrStr = NSAttributedString(string: label, attributes: attrs)
            let textSize = attrStr.size()
            let padX: CGFloat = 12.0
            let padY: CGFloat = 6.5
            let bw = min(bounds.width - 14.0, max(64.0, textSize.width + padX * 2.0))
            let bh = textSize.height + padY * 2.0
            let bx = (bounds.width - bw) / 2.0
            let by = max(10.0, spriteY - bh - 12.0)

            if tasks.count >= 3 {
                let b2 = NSRect(x: bx + 6.0, y: by - 6.0, width: bw - 6.0, height: bh)
                let p2 = NSBezierPath(roundedRect: b2, xRadius: bh / 2.0, yRadius: bh / 2.0)
                NSColor(calibratedRed: 0.08, green: 0.10, blue: 0.16, alpha: 0.65).setFill()
                p2.fill()
            }
            if tasks.count >= 2 {
                let b1 = NSRect(x: bx + 3.0, y: by - 3.0, width: bw - 3.0, height: bh)
                let p1 = NSBezierPath(roundedRect: b1, xRadius: bh / 2.0, yRadius: bh / 2.0)
                NSColor(calibratedRed: 0.08, green: 0.10, blue: 0.16, alpha: 0.80).setFill()
                p1.fill()
            }

            let bubbleRect = NSRect(x: bx, y: by, width: bw, height: bh)
            let path = NSBezierPath(roundedRect: bubbleRect, xRadius: bh / 2.0, yRadius: bh / 2.0)
            NSColor(calibratedRed: 0.09, green: 0.09, blue: 0.13, alpha: 0.95).setFill()
            path.fill()
            accent.setStroke()
            path.lineWidth = isPinned ? 2.2 : 1.8
            path.stroke()

            let tx = bx + (bw - min(textSize.width, bw - padX * 2)) / 2.0
            let ty = by + (bh - textSize.height) / 2.0
            attrStr.draw(in: NSRect(x: tx, y: ty, width: bw - padX * 2, height: textSize.height))
            bubbleHitRects.append((bubbleRect, top.conversationId))
            return
        }

        if let bubbleText = ctrl.currentBubbleText(), !bubbleText.isEmpty {
            let accent = ctrl.accentColor(for: effState)
            let font = NSFont.systemFont(ofSize: 12.5, weight: .bold)
            let attrs: [NSAttributedString.Key: Any] = [
                .font: font,
                .foregroundColor: NSColor.white
            ]
            let attrStr = NSAttributedString(string: bubbleText, attributes: attrs)
            let textSize = attrStr.size()
            let padX: CGFloat = 12.0
            let padY: CGFloat = 7.0
            let bw = min(bounds.width - 12.0, max(54.0, textSize.width + padX * 2.0))
            let bh = textSize.height + padY * 2.0
            let bx = (bounds.width - bw) / 2.0
            let by = max(6.0, spriteY - bh - 12.0)

            let bubbleRect = NSRect(x: bx, y: by, width: bw, height: bh)
            let path = NSBezierPath(roundedRect: bubbleRect, xRadius: bh / 2.0, yRadius: bh / 2.0)
            NSColor(calibratedRed: 0.09, green: 0.09, blue: 0.13, alpha: 0.94).setFill()
            path.fill()
            accent.setStroke()
            path.lineWidth = 2.0
            path.stroke()

            // 小三角
            let tail = NSBezierPath()
            let midX = bounds.width / 2.0
            tail.move(to: NSPoint(x: midX - 6, y: by + bh - 1))
            tail.line(to: NSPoint(x: midX, y: by + bh + 7))
            tail.line(to: NSPoint(x: midX + 6, y: by + bh - 1))
            tail.close()
            NSColor(calibratedRed: 0.09, green: 0.09, blue: 0.13, alpha: 0.94).setFill()
            tail.fill()

            let tx = bx + (bw - min(textSize.width, bw - padX * 2)) / 2.0
            let ty = by + (bh - textSize.height) / 2.0
            attrStr.draw(in: NSRect(x: tx, y: ty, width: bw - padX * 2, height: textSize.height))
        }
    }

    // 鼠标拖拽、气泡悬停展开与点击跳转
    private var dragStartScreen: NSPoint = .zero
    private var lastDragScreenX: CGFloat = 0
    private var windowStartOrigin: NSPoint = .zero
    private var didDrag: Bool = false
    private var bubbleHitRects: [(NSRect, String)] = []
    private var trackingAreaRef: NSTrackingArea?

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let old = trackingAreaRef { removeTrackingArea(old) }
        let area = NSTrackingArea(rect: bounds, options: [.mouseEnteredAndExited, .mouseMoved, .activeAlways], owner: self, userInfo: nil)
        addTrackingArea(area)
        trackingAreaRef = area
    }

    override func mouseMoved(with event: NSEvent) {
        guard let ctrl = controller, ctrl.isDetailedStyle() else { return }
        let pt = convert(event.locationInWindow, from: nil)
        let inBubble = bubbleHitRects.contains { $0.0.insetBy(dx: -8, dy: -8).contains(pt) }
        if inBubble && !ctrl.bubbleExpanded && !ctrl.orderedVisibleTasks().isEmpty {
            ctrl.setBubbleExpanded(true)
        } else if !inBubble && ctrl.bubbleExpanded && pt.y > bounds.height - 150.0 * CGFloat(ctrl.config.Scale) {
            ctrl.setBubbleExpanded(false)
        }
    }

    override func mouseExited(with event: NSEvent) {
        controller?.setBubbleExpanded(false)
    }

    override func mouseDown(with event: NSEvent) {
        guard let win = window else { return }
        dragStartScreen = NSEvent.mouseLocation
        lastDragScreenX = dragStartScreen.x
        windowStartOrigin = win.frame.origin
        didDrag = false
    }

    override func mouseDragged(with event: NSEvent) {
        guard let win = window else { return }
        let cur = NSEvent.mouseLocation
        let dx = cur.x - dragStartScreen.x
        let dy = cur.y - dragStartScreen.y
        let vx = cur.x - lastDragScreenX
        lastDragScreenX = cur.x
        if didDrag || abs(dx) > 4 || abs(dy) > 4 {
            didDrag = true
            win.setFrameOrigin(NSPoint(x: windowStartOrigin.x + dx, y: windowStartOrigin.y + dy))
            controller?.isDragging = true
            controller?.dragTargetAngle = max(-22.0, min(22.0, -vx * 0.85))
            needsDisplay = true
        }
    }

    override func mouseUp(with event: NSEvent) {
        controller?.isDragging = false
        controller?.dragTargetAngle = 0
        if didDrag {
            controller?.saveWindowPosition()
            controller?.overrideRow = PetAnimRow.jump.rawValue
            controller?.overrideUntil = Date().addingTimeInterval(0.6)
        } else {
            let pt = convert(event.locationInWindow, from: nil)
            if let hit = bubbleHitRects.first(where: { $0.0.contains(pt) }) {
                controller?.openConversation(hit.1)
            } else {
                controller?.handleLeftClick()
            }
        }
    }

    override func rightMouseDown(with event: NSEvent) {
        controller?.showContextMenu(with: event, in: self)
    }
}

// MARK: - Pet Window

final class PetWindow: NSWindow {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

// MARK: - Main Controller

final class PetController: NSObject, NSApplicationDelegate {
    var config = PetConfig()
    var packs: [String: PetPack] = [:]
    var conversations: [String: ConvState] = [:]
    var recentConversations: [[String: Any]] = []
    var activeConvId: String = "default"
    var lastConvId: String = "default"
    var activeTitle: String = ""

    var window: PetWindow!
    var canvasView: PetCanvasView!
    var animTimer: Timer?
    var agyCheckTimer: Timer?

    var startedAt = Date()
    var lastTick = Date()
    var animTime: Double = 0
    var overrideRow: Int?
    var overrideUntil: Date = .distantPast
    var walkDir: Int = 0
    var walkUntil: Date = .distantPast
    var nextWalkAt: Date = Date().addingTimeInterval(14)
    var lookIndex: Int = -1
    var lastSoundState: String = "idle"
    var lastSoundAt: Date = .distantPast
    var visible: Bool = true
    var isDragging: Bool = false
    var dragTargetAngle: CGFloat = 0
    var dragCurrentAngle: CGFloat = 0
    var dragScaleX: CGFloat = 1.0
    var dragScaleY: CGFloat = 1.0
    var bubbleExpanded: Bool = false

    let port: UInt16 = 47821
    var serverSocket: Int32 = -1
    var hotKeyRef: EventHotKeyRef?

    lazy var pluginRoot: URL = {
        let exeUrl = URL(fileURLWithPath: CommandLine.arguments[0]).resolvingSymlinksInPath()
        // If inside app/AntigravityPet.app/Contents/MacOS/AntigravityPet -> go up 4 levels
        let path = exeUrl.path
        if path.contains(".app/Contents/MacOS") {
            return exeUrl.deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
        }
        return exeUrl.deletingLastPathComponent().deletingLastPathComponent()
    }()

    lazy var configUrl: URL = {
        let appSup = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let dir = appSup.appendingPathComponent("AntigravityPet")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir.appendingPathComponent("config.json")
    }()

    lazy var userPetsDir: URL = {
        return FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent(".antigravity-pet")
            .appendingPathComponent("pets")
    }()

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        loadConfig()
        loadPetPacks()
        setupWindow()
        setupGlobalHotkey()
        startHttpServer()

        animTimer = Timer.scheduledTimer(withTimeInterval: 0.05, repeats: true) { [weak self] _ in
            self?.onTick()
        }
        agyCheckTimer = Timer.scheduledTimer(withTimeInterval: 4.0, repeats: true) { [weak self] _ in
            self?.checkAntigravityAlive()
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        saveConfig()
        if serverSocket >= 0 { Darwin.close(serverSocket) }
    }

    // MARK: Config

    func loadConfig() {
        guard let data = try? Data(contentsOf: configUrl),
              let decoded = try? JSONDecoder().decode(PetConfig.self, from: data) else {
            saveConfig()
            return
        }
        config = decoded
        if !config.Scale.isFinite || config.Scale < 0.5 || config.Scale > 2.5 { config.Scale = 1.0 }
        if !config.Left.isFinite { config.Left = -1 }
        if !config.Top.isFinite { config.Top = -1 }
    }

    func saveConfig() {
        if !config.Left.isFinite { config.Left = -1 }
        if !config.Top.isFinite { config.Top = -1 }
        if !config.Scale.isFinite { config.Scale = 1.0 }
        let enc = JSONEncoder()
        enc.outputFormatting = [.prettyPrinted]
        if let data = try? enc.encode(config) {
            try? data.write(to: configUrl)
        }
    }

    // MARK: Pets

    func loadPetPacks() {
        packs.removeAll()
        let builtIn = pluginRoot.appendingPathComponent("pets")
        for dir in [builtIn, userPetsDir] {
            guard let items = try? FileManager.default.contentsOfDirectory(at: dir, includingPropertiesForKeys: nil) else { continue }
            for item in items {
                let manifest = item.appendingPathComponent("pet.json")
                guard let data = try? Data(contentsOf: manifest),
                      let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { continue }
                let id = (obj["id"] as? String) ?? item.lastPathComponent
                let dn = (obj["displayName"] as? String) ?? id
                let desc = (obj["description"] as? String) ?? ""
                let ver = (obj["spriteVersionNumber"] as? Int) ?? 2
                let rel = (obj["spritesheetPath"] as? String) ?? "spritesheet.png"
                let sheetPath = item.appendingPathComponent(rel).path
                if let img = NSImage(contentsOfFile: sheetPath) {
                    packs[id] = PetPack(id: id, displayName: dn, description: desc, version: ver, spritesheetPath: sheetPath, image: img)
                }
            }
        }
        if packs[config.Skin] == nil, let first = packs.keys.sorted().first {
            config.Skin = first
        }
    }

    func activePack() -> PetPack? {
        return packs[config.Skin] ?? packs.values.first
    }

    // MARK: Window

    func windowSizeForScale(_ s: Double) -> NSSize {
        let w = max(180.0, 220.0 * s)
        let h = max(190.0, 230.0 * s)
        return NSSize(width: w, height: h)
    }

    func setupWindow() {
        let sz = windowSizeForScale(config.Scale)
        let screen = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1440, height: 900)
        var origin = NSPoint(x: screen.maxX - sz.width - 36, y: screen.minY + 28)

        if config.Left >= 0 && config.Top >= 0 {
            let macY = (NSScreen.main?.frame.height ?? 900) - config.Top - sz.height
            let candidate = NSPoint(x: config.Left, y: macY)
            if screen.intersects(NSRect(origin: candidate, size: sz)) {
                origin = candidate
            }
        }

        window = PetWindow(
            contentRect: NSRect(origin: origin, size: sz),
            styleMask: [.borderless],
            backing: .buffered,
            defer: false
        )
        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = false
        window.level = config.Topmost ? .floating : .normal
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .ignoresCycle]

        canvasView = PetCanvasView(frame: NSRect(origin: .zero, size: sz))
        canvasView.controller = self
        window.contentView = canvasView
        window.orderFrontRegardless()
        visible = true
    }

    func saveWindowPosition() {
        guard let win = window else { return }
        let screenH = NSScreen.main?.frame.height ?? 900
        config.Left = Double(win.frame.origin.x)
        config.Top = Double(screenH - win.frame.origin.y - win.frame.height)
        saveConfig()
    }

    func applyScale(_ newScale: Double) {
        let s = min(2.0, max(0.6, newScale))
        config.Scale = s
        let oldFrame = window.frame
        let sz = windowSizeForScale(s)
        let newOrigin = NSPoint(x: oldFrame.midX - sz.width / 2.0, y: oldFrame.minY)
        window.setFrame(NSRect(origin: newOrigin, size: sz), display: true)
        canvasView.frame = NSRect(origin: .zero, size: sz)
        saveWindowPosition()
    }

    func setPetVisible(_ show: Bool) {
        visible = show
        if show {
            window.orderFrontRegardless()
        } else {
            window.orderOut(nil)
        }
    }

    // MARK: Global Hotkey (Option + Command + P)

    func setupGlobalHotkey() {
        var eventType = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let selfPtr = Unmanaged.passUnretained(self).toOpaque()
        InstallEventHandler(GetApplicationEventTarget(), { (_, _, userData) -> OSStatus in
            guard let ptr = userData else { return noErr }
            let ctrl = Unmanaged<PetController>.fromOpaque(ptr).takeUnretainedValue()
            DispatchQueue.main.async {
                ctrl.setPetVisible(!ctrl.visible)
            }
            return noErr
        }, 1, &eventType, selfPtr, nil)

        var hotKeyID = EventHotKeyID(signature: 0x41504554, id: 1) // 'APET'
        RegisterEventHotKey(UInt32(kVK_ANSI_P), UInt32(optionKey | cmdKey), hotKeyID, GetApplicationEventTarget(), 0, &hotKeyRef)
    }

    // MARK: Antigravity Lifecycle & Click-to-Jump

    func checkAntigravityAlive() {
        guard config.AutoStartOnBoot else { return }
        if Date().timeIntervalSince(startedAt) < 20.0 { return }
        let running = NSWorkspace.shared.runningApplications.contains { app in
            let name = (app.localizedName ?? "").lowercased()
            let bid = (app.bundleIdentifier ?? "").lowercased()
            return name.contains("antigravity") || bid.contains("antigravity")
        }
        if !running {
            saveConfig()
            NSApp.terminate(nil)
        }
    }

    func handleLeftClick() {
        overrideRow = PetAnimRow.wave.rawValue
        overrideUntil = Date().addingTimeInterval(0.9)
        let targetId = activeConvId != "default" ? activeConvId : lastConvId
        openConversation(targetId)
    }

    func openConversation(_ convId: String) {
        for app in NSWorkspace.shared.runningApplications {
            let name = (app.localizedName ?? "").lowercased()
            let bid = (app.bundleIdentifier ?? "").lowercased()
            if name.contains("antigravity") || bid.contains("antigravity") {
                app.activate(options: [.activateAllWindows, .activateIgnoringOtherApps])
                break
            }
        }
        if !convId.isEmpty && convId != "default" && convId != "preview" {
            if let url = URL(string: "antigravity://conversation/\(convId)") {
                NSWorkspace.shared.open(url)
            }
        }
    }

    // MARK: State Arbitration & Animation Tick

    func priorityScore(_ state: String) -> Int {
        switch state {
        case "needs_input": return 1
        case "blocked": return 2
        case "ready": return 3
        case "running": return 4
        default: return 5
        }
    }

    func isDetailedStyle() -> Bool {
        return config.BubbleStyle.lowercased() == "detailed"
    }

    func setBubbleExpanded(_ expand: Bool) {
        if bubbleExpanded == expand { return }
        bubbleExpanded = expand
        let baseSz = windowSizeForScale(config.Scale)
        let tasks = orderedVisibleTasks()
        let showCount = expand ? min(5, tasks.count) : 1
        let extraH = expand && showCount > 1 ? CGFloat(showCount - 1) * 31.0 : 0.0
        var f = window.frame
        let newH = baseSz.height + extraH
        f.size.height = newH
        window.setFrame(f, display: true)
        canvasView.frame = NSRect(origin: .zero, size: f.size)
        canvasView.needsDisplay = true
    }

    func orderedVisibleTasks() -> [ConvState] {
        let now = Date()
        var list: [ConvState] = []
        for (cid, var cs) in conversations {
            let age = now.timeIntervalSince(cs.updatedAt)
            if cs.state == "running" && age > 45 { cs.state = "idle"; conversations[cid] = cs }
            if cs.state == "ready" && age > 25 { cs.state = "idle"; conversations[cid] = cs }
            if cs.state == "idle" { continue }
            if config.BubbleMode == "alerts" && cs.state != "needs_input" && cs.state != "blocked" && cs.state != "ready" { continue }
            list.append(cs)
        }
        return list.sorted { a, b in
            let pa = (!config.PinnedConversationId.isEmpty && a.conversationId == config.PinnedConversationId) ? 0 : 1
            let pb = (!config.PinnedConversationId.isEmpty && b.conversationId == config.PinnedConversationId) ? 0 : 1
            if pa != pb { return pa < pb }
            let sa = priorityScore(a.state)
            let sb = priorityScore(b.state)
            if sa != sb { return sa < sb }
            return a.updatedAt > b.updatedAt
        }
    }

    func formatDetailedTaskLabel(_ cs: ConvState) -> String {
        let lang = I18n.effectiveLanguage(config: config)
        let rawTitle = cs.title.trimmingCharacters(in: .whitespacesAndNewlines)
        let taskName: String
        if !rawTitle.isEmpty {
            taskName = rawTitle.count > 12 ? String(rawTitle.prefix(12)) + "…" : rawTitle
        } else if cs.conversationId == "default" || cs.conversationId.isEmpty {
            taskName = "Antigravity"
        } else {
            taskName = String(cs.conversationId.prefix(8))
        }
        let stLabel = I18n.translateBubble(state: cs.state, rawMessage: cs.message, lang: lang)
        return "\(taskName) - \(stLabel)"
    }

    func currentTopConversation() -> ConvState? {
        let now = Date()
        if !config.PinnedConversationId.isEmpty, var pinned = conversations[config.PinnedConversationId] {
            let age = now.timeIntervalSince(pinned.updatedAt)
            if pinned.state == "running" && age > 45 { pinned.state = "idle"; conversations[config.PinnedConversationId] = pinned }
            if pinned.state == "ready" && age > 25 { pinned.state = "idle"; conversations[config.PinnedConversationId] = pinned }
            if pinned.state != "idle" { return pinned }
        }
        var best: ConvState?
        for (cid, var cs) in conversations {
            let age = now.timeIntervalSince(cs.updatedAt)
            if cs.state == "running" && age > 45 { cs.state = "idle"; conversations[cid] = cs }
            if cs.state == "ready" && age > 25 { cs.state = "idle"; conversations[cid] = cs }
            if cs.state == "idle" { continue }
            if let cur = best {
                let pNew = priorityScore(cs.state)
                let pCur = priorityScore(cur.state)
                if pNew < pCur || (pNew == pCur && cs.updatedAt > cur.updatedAt) {
                    best = cs
                }
            } else {
                best = cs
            }
        }
        return best
    }

    func currentEffectiveState() -> String {
        return currentTopConversation()?.state ?? "idle"
    }

    func currentBubbleText() -> String? {
        let mode = config.BubbleMode
        if mode == "off" { return nil }
        let st = currentEffectiveState()
        if mode == "alerts" && st != "needs_input" && st != "blocked" && st != "ready" { return nil }
        guard let top = currentTopConversation() else { return nil }
        let lang = I18n.effectiveLanguage(config: config)
        return I18n.translateBubble(state: top.state, rawMessage: top.message, lang: lang)
    }

    func accentColor(for state: String) -> NSColor {
        switch state {
        case "needs_input": return NSColor(calibratedRed: 1.0, green: 0.70, blue: 0.0, alpha: 1.0)
        case "blocked": return NSColor(calibratedRed: 1.0, green: 0.35, blue: 0.37, alpha: 1.0)
        case "ready": return NSColor(calibratedRed: 0.30, green: 0.85, blue: 0.39, alpha: 1.0)
        case "running": return NSColor(calibratedRed: 0.31, green: 0.76, blue: 0.97, alpha: 1.0)
        default: return NSColor(calibratedRed: 0.65, green: 0.55, blue: 0.98, alpha: 1.0)
        }
    }

    func currentRowAndFrame() -> (Int, Int) {
        let rowFrames = [6, 8, 8, 4, 5, 8, 6, 6, 6, 8, 8]
        let now = Date()
        var row = 0
        if isDragging {
            let maxF = rowFrames[PetAnimRow.jump.rawValue]
            let frame = Int(animTime * 12.0) % max(1, maxF)
            return (PetAnimRow.jump.rawValue, frame)
        }
        if now < overrideUntil, let ov = overrideRow {
            row = ov
        } else {
            let st = currentEffectiveState()
            switch st {
            case "running": row = PetAnimRow.running.rawValue
            case "needs_input": row = PetAnimRow.needsInput.rawValue
            case "blocked": row = PetAnimRow.blocked.rawValue
            case "ready": row = PetAnimRow.ready.rawValue
            default:
                if walkDir != 0 && now < walkUntil {
                    row = walkDir > 0 ? PetAnimRow.walkRight.rawValue : PetAnimRow.walkLeft.rawValue
                } else if config.EyeFollow && lookIndex >= 0 {
                    let lr = lookIndex < 8 ? 9 : 10
                    let lf = lookIndex % 8
                    return (lr, lf)
                } else {
                    row = PetAnimRow.idle.rawValue
                }
            }
        }
        let maxF = rowFrames[min(row, rowFrames.count - 1)]
        let frame = Int(animTime * 9.0) % max(1, maxF)
        return (row, frame)
    }

    func onTick() {
        let now = Date()
        let dt = min(0.2, now.timeIntervalSince(lastTick))
        lastTick = now
        animTime += dt

        if isDragging {
            let dangle = CGFloat(sin(animTime * 7.0) * 3.5)
            let target = dragTargetAngle + dangle
            dragCurrentAngle += (target - dragCurrentAngle) * 0.35
            dragTargetAngle *= 0.78
            dragScaleX += (0.94 - dragScaleX) * 0.35
            dragScaleY += (1.07 - dragScaleY) * 0.35
        } else if abs(dragCurrentAngle) > 0.1 || abs(dragScaleX - 1.0) > 0.005 {
            dragCurrentAngle *= 0.65
            if abs(dragCurrentAngle) < 0.1 { dragCurrentAngle = 0 }
            dragScaleX += (1.0 - dragScaleX) * 0.35
            dragScaleY += (1.0 - dragScaleY) * 0.35
        }

        let st = currentEffectiveState()
        if st == "idle" {
            // 待机散步
            if config.AutoWalk {
                if walkDir != 0 && now < walkUntil {
                    if let screen = NSScreen.main?.visibleFrame {
                        var f = window.frame
                        let nx = f.origin.x + CGFloat(walkDir) * 34.0 * CGFloat(dt)
                        if nx <= screen.minX + 12 { walkDir = 1 }
                        else if nx + f.width >= screen.maxX - 12 { walkDir = -1 }
                        else { f.origin.x = nx; window.setFrameOrigin(f.origin) }
                    }
                } else if walkDir != 0 && now >= walkUntil {
                    walkDir = 0
                    nextWalkAt = now.addingTimeInterval(Double.random(in: 12...22))
                    saveWindowPosition()
                } else if now >= nextWalkAt {
                    walkDir = Bool.random() ? 1 : -1
                    walkUntil = now.addingTimeInterval(Double.random(in: 2.5...4.5))
                }
            } else {
                walkDir = 0
            }

            // 16 方向眼神跟随
            if config.EyeFollow && walkDir == 0 {
                let mouse = NSEvent.mouseLocation
                let center = NSPoint(x: window.frame.midX, y: window.frame.midY)
                let dx = mouse.x - center.x
                let dy = center.y - mouse.y // 转为向下为正
                let dist = hypot(dx, dy)
                if dist > 45 && dist < 560 {
                    var ang = atan2(dy, dx)
                    if ang < 0 { ang += 2 * .pi }
                    lookIndex = Int(round(ang / (2 * .pi / 16.0))) % 16
                } else {
                    lookIndex = -1
                }
            } else {
                lookIndex = -1
            }
        } else {
            walkDir = 0
            lookIndex = -1
        }

        if visible {
            canvasView.needsDisplay = true
        }
    }

    // MARK: Context Menu

    func showContextMenu(with event: NSEvent, in view: NSView) {
        let menu = NSMenu()
        let lang = I18n.effectiveLanguage(config: config)
        let isEn = lang == "en"

        let skinMenu = NSMenu()
        for id in packs.keys.sorted() {
            let p = packs[id]!
            let dn = (isEn ? I18n.enPetMeta[id]?.0 : nil) ?? p.displayName
            let mi = NSMenuItem(title: dn, action: #selector(menuSelectSkin(_:)), keyEquivalent: "")
            mi.representedObject = id
            mi.state = (id == config.Skin) ? .on : .off
            mi.target = self
            skinMenu.addItem(mi)
        }
        let skinRoot = NSMenuItem(title: isEn ? "Switch Pet" : "切换桌宠角色", action: nil, keyEquivalent: "")
        skinRoot.submenu = skinMenu
        menu.addItem(skinRoot)

        let openItem = NSMenuItem(title: isEn ? "Open Active Chat" : "打开关联对话", action: #selector(menuOpenChat), keyEquivalent: "")
        openItem.target = self
        menu.addItem(openItem)

        let hideItem = NSMenuItem(title: isEn ? "Hide (⌥⌘P)" : "隐藏桌宠 (⌥⌘P)", action: #selector(menuHide), keyEquivalent: "")
        hideItem.target = self
        menu.addItem(hideItem)

        menu.addItem(.separator())

        let quitItem = NSMenuItem(title: isEn ? "Quit Pet" : "退出桌宠", action: #selector(menuQuit), keyEquivalent: "")
        quitItem.target = self
        menu.addItem(quitItem)

        NSMenu.popUpContextMenu(menu, with: event, for: view)
    }

    @objc func menuSelectSkin(_ sender: NSMenuItem) {
        if let id = sender.representedObject as? String, packs[id] != nil {
            config.Skin = id
            saveConfig()
            canvasView.needsDisplay = true
        }
    }

    @objc func menuOpenChat() {
        let targetId = activeConvId != "default" ? activeConvId : lastConvId
        openConversation(targetId)
    }

    @objc func menuHide() { setPetVisible(false) }
    @objc func menuQuit() { saveConfig(); NSApp.terminate(nil) }

    // MARK: HTTP Server (POSIX Socket 127.0.0.1:47821)

    func startHttpServer() {
        DispatchQueue.global(qos: .utility).async { [weak self] in
            guard let self = self else { return }
            let fd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
            guard fd >= 0 else { return }
            var yes: Int32 = 1
            setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &yes, socklen_t(MemoryLayout<Int32>.size))

            var addr = sockaddr_in()
            addr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
            addr.sin_family = sa_family_t(AF_INET)
            addr.sin_port = self.port.bigEndian
            addr.sin_addr.s_addr = inet_addr("127.0.0.1")

            let bindRes = withUnsafePointer(to: &addr) {
                $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                    Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
                }
            }
            guard bindRes == 0, Darwin.listen(fd, 16) == 0 else {
                Darwin.close(fd)
                return
            }
            self.serverSocket = fd

            while true {
                let client = Darwin.accept(fd, nil, nil)
                if client < 0 { break }
                DispatchQueue.global(qos: .userInitiated).async {
                    self.handleClient(client)
                }
            }
        }
    }

    func handleClient(_ client: Int32) {
        defer { Darwin.close(client) }
        var buf = [UInt8](repeating: 0, count: 16384)
        let n = Darwin.recv(client, &buf, buf.count, 0)
        guard n > 0 else { return }
        let rawStr = String(decoding: buf[0..<n], as: UTF8.self)
        let parts = rawStr.components(separatedBy: "\r\n\r\n")
        let head = parts.first ?? ""
        var bodyStr = parts.count > 1 ? parts.dropFirst().joined(separator: "\r\n\r\n") : ""

        let lines = head.components(separatedBy: "\r\n")
        let reqLine = (lines.first ?? "").components(separatedBy: " ")
        let method = reqLine.count > 0 ? reqLine[0] : "GET"
        let path = (reqLine.count > 1 ? reqLine[1] : "/").components(separatedBy: "?").first ?? "/"

        var contentLength = 0
        for line in lines.dropFirst() {
            if line.lowercased().hasPrefix("content-length:") {
                contentLength = Int(line.dropFirst(15).trimmingCharacters(in: .whitespaces)) ?? 0
            }
        }
        while bodyStr.utf8.count < contentLength {
            let m = Darwin.recv(client, &buf, buf.count, 0)
            if m <= 0 { break }
            bodyStr += String(decoding: buf[0..<m], as: UTF8.self)
        }

        var jsonBody: [String: Any] = [:]
        if let bd = bodyStr.data(using: .utf8),
           let obj = try? JSONSerialization.jsonObject(with: bd) as? [String: Any] {
            jsonBody = obj
        }

        var respObj: [String: Any] = ["ok": true]
        if method == "OPTIONS" {
            sendHttpResponse(client, status: "204 No Content", body: "")
            return
        }

        DispatchQueue.main.sync {
            respObj = self.routeHttp(method: method, path: path, body: jsonBody)
        }

        let respData = (try? JSONSerialization.data(withJSONObject: respObj)) ?? Data("{\"ok\":true}".utf8)
        let respJson = String(data: respData, encoding: .utf8) ?? "{\"ok\":true}"
        sendHttpResponse(client, status: "200 OK", body: respJson)
    }

    func sendHttpResponse(_ client: Int32, status: String, body: String) {
        let bytes = Array(body.utf8)
        let header = "HTTP/1.1 \(status)\r\nContent-Type: application/json; charset=utf-8\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nContent-Length: \(bytes.count)\r\nConnection: close\r\n\r\n"
        _ = header.withCString { Darwin.send(client, $0, Int(strlen($0)), 0) }
        if !bytes.isEmpty {
            _ = bytes.withUnsafeBytes { Darwin.send(client, $0.baseAddress, bytes.count, 0) }
        }
    }

    func routeHttp(method: String, path: String, body: [String: Any]) -> [String: Any] {
        if path == "/state" && method == "POST" {
            let cid = (body["conversationId"] as? String) ?? "default"
            let st = ((body["state"] as? String) ?? "idle").lowercased()
            let msg = (body["message"] as? String) ?? ""
            let title = (body["title"] as? String) ?? ""
            let isTool = (body["isToolCall"] as? Bool) ?? false

            let prev = conversations[cid]?.state ?? "idle"
            conversations[cid] = ConvState(conversationId: cid, state: st, message: msg, title: title, updatedAt: Date())
            if cid != "default" { lastConvId = cid }
            activeConvId = cid
            if !title.isEmpty { activeTitle = title }

            if isTool { config.ToolCallsCount += 1 }
            if st == "ready" && prev != "ready" { config.CompletedTasksCount += 1 }

            if !title.isEmpty && cid != "default" && cid != "preview" {
                recentConversations.removeAll { ($0["id"] as? String) == cid }
                recentConversations.insert([
                    "id": cid,
                    "title": title,
                    "state": st,
                    "updatedAt": ISO8601DateFormatter().string(from: Date())
                ], at: 0)
                if recentConversations.count > 8 { recentConversations = Array(recentConversations.prefix(8)) }
            }

            if config.SoundAlert && (st == "needs_input" || st == "blocked" || st == "ready") && (st != lastSoundState || Date().timeIntervalSince(lastSoundAt) > 3.0) {
                let sName = st == "blocked" ? "Basso" : (st == "needs_input" ? "Ping" : "Glass")
                NSSound(named: NSSound.Name(sName))?.play()
                lastSoundState = st
                lastSoundAt = Date()
            }
            canvasView.needsDisplay = true
            return healthSnapshot()
        }

        if path == "/skin" && method == "POST" {
            if let sk = body["skin"] as? String, packs[sk] != nil {
                config.Skin = sk
                saveConfig()
                canvasView.needsDisplay = true
            }
            return healthSnapshot()
        }

        if path == "/reload" && method == "POST" {
            loadPetPacks()
            canvasView.needsDisplay = true
            return healthSnapshot()
        }

        if path == "/control" && method == "POST" {
            let action = ((body["action"] as? String) ?? "").lowercased()
            let val = (body["value"] as? String) ?? ""
            switch action {
            case "show": setPetVisible(true)
            case "hide": setPetVisible(false)
            case "toggle": setPetVisible(!visible)
            case "open": openConversation(val.isEmpty ? (activeConvId != "default" ? activeConvId : lastConvId) : val)
            case "quit":
                saveConfig()
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.1) { NSApp.terminate(nil) }
            case "resetpos":
                config.Left = -1; config.Top = -1
                if let scr = NSScreen.main?.visibleFrame {
                    let sz = window.frame.size
                    window.setFrameOrigin(NSPoint(x: scr.maxX - sz.width - 36, y: scr.minY + 28))
                }
                saveWindowPosition()
            case "wave":
                overrideRow = PetAnimRow.wave.rawValue
                overrideUntil = Date().addingTimeInterval(1.2)
            case "jump":
                overrideRow = PetAnimRow.jump.rawValue
                overrideUntil = Date().addingTimeInterval(1.1)
            case "walk":
                walkDir = Bool.random() ? 1 : -1
                walkUntil = Date().addingTimeInterval(3.5)
            case "scale":
                if let d = Double(val) { applyScale(d); saveConfig() }
            case "bubblemode":
                config.BubbleMode = val.isEmpty ? "always" : val; saveConfig()
            case "bubblestyle":
                if val == "simple" || val == "detailed" {
                    config.BubbleStyle = val
                    if val == "simple" { setBubbleExpanded(false) }
                    saveConfig()
                }
            case "pinconversation":
                config.PinnedConversationId = val; saveConfig()
            case "autowalk":
                config.AutoWalk = (val.lowercased() == "true" || val == "1"); saveConfig()
            case "eyefollow":
                config.EyeFollow = (val.lowercased() == "true" || val == "1"); saveConfig()
            case "topmost":
                let tm = (val.lowercased() == "true" || val == "1")
                config.Topmost = tm
                window.level = tm ? .floating : .normal
                saveConfig()
            case "autostartonboot":
                config.AutoStartOnBoot = (val.lowercased() == "true" || val == "1"); saveConfig()
            case "soundalert":
                config.SoundAlert = (val.lowercased() == "true" || val == "1"); saveConfig()
            case "language":
                config.Language = val.isEmpty ? "auto" : val.lowercased(); saveConfig()
            case "agylocale":
                config.AgyDetectedLocale = val.lowercased(); saveConfig()
            default: break
            }
            canvasView.needsDisplay = true
            return healthSnapshot()
        }

        return healthSnapshot()
    }

    func healthSnapshot() -> [String: Any] {
        let lang = I18n.effectiveLanguage(config: config)
        let isEn = lang == "en"
        let pack = activePack()
        let dn = (isEn ? I18n.enPetMeta[config.Skin]?.0 : nil) ?? pack?.displayName ?? config.Skin
        let petsList: [[String: Any]] = packs.keys.sorted().map { id in
            let p = packs[id]!
            let en = I18n.enPetMeta[id]
            return [
                "id": id,
                "displayName": (isEn && en != nil) ? en!.0 : p.displayName,
                "description": (isEn && en != nil) ? en!.1 : p.description,
                "version": p.version
            ]
        }
        let activeCount = conversations.values.filter { $0.state != "idle" }.count
        let top = currentTopConversation()
        return [
            "ok": true,
            "platform": "darwin",
            "running": true,
            "enabled": config.Enabled,
            "state": top?.state ?? "idle",
            "skin": config.Skin,
            "displayName": dn,
            "visible": visible,
            "scale": config.Scale,
            "bubbleMode": config.BubbleMode,
            "bubbleStyle": isDetailedStyle() ? "detailed" : "simple",
            "pinnedConversationId": config.PinnedConversationId,
            "autoWalk": config.AutoWalk,
            "eyeFollow": config.EyeFollow,
            "topmost": config.Topmost,
            "autoStartOnBoot": config.AutoStartOnBoot,
            "soundAlert": config.SoundAlert,
            "language": config.Language,
            "effectiveLanguage": lang,
            "activeTasks": activeCount,
            "completedTasksCount": config.CompletedTasksCount,
            "toolCallsCount": config.ToolCallsCount,
            "uptimeMinutes": Int(Date().timeIntervalSince(startedAt) / 60.0),
            "activeConversationId": top?.conversationId ?? activeConvId,
            "lastConversationId": lastConvId,
            "activeTitle": top?.title ?? activeTitle,
            "recentConversations": recentConversations,
            "availablePets": petsList
        ]
    }
}

// MARK: - Entry Point

let app = NSApplication.shared
let delegate = PetController()
app.delegate = delegate
app.run()
