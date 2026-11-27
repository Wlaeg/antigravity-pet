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
    var ReadyTimeoutSec: Int = 30
    var Scale: Double = 1.0
    var AutoWalk: Bool = true
    var EyeFollow: Bool = true
    var AutoStartOnBoot: Bool = true
    var SoundAlert: Bool = false
    var Language: String = "auto"
    var AgyDetectedLocale: String = ""
    var CompletedTasksCount: Int = 0
    var ToolCallsCount: Int = 0
    var SnapToAgy: Bool = false
    var HideOnFullscreen: Bool = true
    var CheckUpdatesOnStartup: Bool = true
    var Accessory: String = "auto"

    init() {}

    enum CodingKeys: String, CodingKey {
        case Enabled, Skin, Left, Top, Topmost, BubbleMode, BubbleStyle
        case PinnedConversationId, ReadyTimeoutSec, Scale, AutoWalk, EyeFollow
        case AutoStartOnBoot, SoundAlert, Language, AgyDetectedLocale
        case CompletedTasksCount, ToolCallsCount, SnapToAgy, HideOnFullscreen
        case CheckUpdatesOnStartup, Accessory
    }

    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        Enabled = (try? c.decodeIfPresent(Bool.self, forKey: .Enabled)) ?? true
        Skin = (try? c.decodeIfPresent(String.self, forKey: .Skin)) ?? "cyber-fox"
        Left = (try? c.decodeIfPresent(Double.self, forKey: .Left)) ?? -1
        Top = (try? c.decodeIfPresent(Double.self, forKey: .Top)) ?? -1
        Topmost = (try? c.decodeIfPresent(Bool.self, forKey: .Topmost)) ?? true
        BubbleMode = (try? c.decodeIfPresent(String.self, forKey: .BubbleMode)) ?? "always"
        BubbleStyle = (try? c.decodeIfPresent(String.self, forKey: .BubbleStyle)) ?? "simple"
        PinnedConversationId = (try? c.decodeIfPresent(String.self, forKey: .PinnedConversationId)) ?? ""
        ReadyTimeoutSec = (try? c.decodeIfPresent(Int.self, forKey: .ReadyTimeoutSec)) ?? 30
        Scale = (try? c.decodeIfPresent(Double.self, forKey: .Scale)) ?? 1.0
        AutoWalk = (try? c.decodeIfPresent(Bool.self, forKey: .AutoWalk)) ?? true
        EyeFollow = (try? c.decodeIfPresent(Bool.self, forKey: .EyeFollow)) ?? true
        AutoStartOnBoot = (try? c.decodeIfPresent(Bool.self, forKey: .AutoStartOnBoot)) ?? true
        SoundAlert = (try? c.decodeIfPresent(Bool.self, forKey: .SoundAlert)) ?? false
        Language = (try? c.decodeIfPresent(String.self, forKey: .Language)) ?? "auto"
        AgyDetectedLocale = (try? c.decodeIfPresent(String.self, forKey: .AgyDetectedLocale)) ?? ""
        CompletedTasksCount = (try? c.decodeIfPresent(Int.self, forKey: .CompletedTasksCount)) ?? 0
        ToolCallsCount = (try? c.decodeIfPresent(Int.self, forKey: .ToolCallsCount)) ?? 0
        SnapToAgy = (try? c.decodeIfPresent(Bool.self, forKey: .SnapToAgy)) ?? false
        HideOnFullscreen = (try? c.decodeIfPresent(Bool.self, forKey: .HideOnFullscreen)) ?? true
        CheckUpdatesOnStartup = (try? c.decodeIfPresent(Bool.self, forKey: .CheckUpdatesOnStartup)) ?? true
        Accessory = (try? c.decodeIfPresent(String.self, forKey: .Accessory)) ?? "auto"
    }
}

struct PetPack {
    let id: String
    let displayName: String
    let description: String
    let version: Int
    let spritesheetPath: String
    var image: NSImage?
    var themeColorHex: String = "#38BDF8"
    var sounds: [String: String] = [:]
    var frameMap: [Int: [CGImage]] = [:]
    var isCustom: Bool = false
}

final class PendingApproval {
    let id: String
    let toolName: String
    let summary: String
    let conversationId: String
    let semaphore = DispatchSemaphore(value: 0)
    var decision: String = "ask"
    var resolved: Bool = false
    init(id: String, toolName: String, summary: String, conversationId: String) {
        self.id = id
        self.toolName = toolName
        self.summary = summary
        self.conversationId = conversationId
    }
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

        // 绘制精灵图（严格最近邻无插值，支持分动作目录序列帧回退）
        if let pack = ctrl.activePack() {
            let (row, frame) = ctrl.currentRowAndFrame()
            var cropped: CGImage? = nil
            if let seqFrames = pack.frameMap[row], !seqFrames.isEmpty {
                cropped = seqFrames[frame % seqFrames.count]
            } else if let nsImg = pack.image, let cgImg = nsImg.cgImage(forProposedRect: nil, context: nil, hints: nil) {
                let cellW = 192
                let cellH = 208
                let cropRect = CGRect(x: frame * cellW, y: row * cellH, width: cellW, height: cellH)
                cropped = cgImg.cropping(to: cropRect)
            }
            if let frameImg = cropped {
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

                ctx.saveGState()
                ctx.translateBy(x: destRect.minX, y: destRect.maxY)
                ctx.scaleBy(x: 1.0, y: -1.0)
                ctx.draw(frameImg, in: CGRect(x: 0, y: 0, width: destRect.width, height: destRect.height))
                ctx.restoreGState()

                // 轻度成长像素配饰直接融入精灵动画与变换层
                let acc = ctrl.effectiveAccessory()
                if acc != "none" {
                    drawPixelAccessory(ctx: ctx, accessory: acc, skinId: pack.id, row: row, frame: frame, destRect: destRect)
                }
                ctx.restoreGState()
            }
        }

        // 绘制头顶像素风气泡 & 审批按钮
        bubbleHitRects.removeAll()
        approvalHitRects.removeAll()

        if let pending = ctrl.currentApproval {
            let isEn = I18n.effectiveLanguage(config: ctrl.config) == "en"
            let title = (isEn ? "⚠ Approve: " : "⚠ 审批请求: ") + pending.toolName
            let font = NSFont.monospacedSystemFont(ofSize: 11.0, weight: .bold)
            let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: NSColor.white]
            let attrStr = NSAttributedString(string: title, attributes: attrs)
            let textSize = attrStr.size()
            let bw = min(bounds.width - 10.0, max(168.0, textSize.width + 24.0))
            let bh: CGFloat = 58.0
            let bx = (bounds.width - bw) / 2.0
            let by = max(6.0, spriteY - bh - 10.0)
            let rect = NSRect(x: bx, y: by, width: bw, height: bh)
            drawPixelBox(rect: rect, fill: NSColor(calibratedRed: 0.06, green: 0.09, blue: 0.16, alpha: 0.96), border: NSColor(calibratedRed: 0.96, green: 0.62, blue: 0.04, alpha: 1.0), lineWidth: 2.0)
            attrStr.draw(in: NSRect(x: bx + 10.0, y: by + 6.0, width: bw - 20.0, height: 18.0))

            let btnW = (bw - 28.0) / 2.0
            let allowRect = NSRect(x: bx + 10.0, y: by + 28.0, width: btnW, height: 22.0)
            let denyRect = NSRect(x: bx + 18.0 + btnW, y: by + 28.0, width: btnW, height: 22.0)
            drawPixelBox(rect: allowRect, fill: NSColor(calibratedRed: 0.06, green: 0.72, blue: 0.51, alpha: 1.0), border: NSColor(calibratedRed: 0.02, green: 0.47, blue: 0.34, alpha: 1.0), lineWidth: 1.5)
            drawPixelBox(rect: denyRect, fill: NSColor(calibratedRed: 0.94, green: 0.27, blue: 0.27, alpha: 1.0), border: NSColor(calibratedRed: 0.60, green: 0.11, blue: 0.11, alpha: 1.0), lineWidth: 1.5)

            let btnFont = NSFont.monospacedSystemFont(ofSize: 10.5, weight: .bold)
            let allowStr = NSAttributedString(string: isEn ? "✓ Allow" : "✓ 允许", attributes: [.font: btnFont, .foregroundColor: NSColor.white])
            let denyStr = NSAttributedString(string: isEn ? "✕ Deny" : "✕ 拒绝", attributes: [.font: btnFont, .foregroundColor: NSColor.white])
            allowStr.draw(in: NSRect(x: allowRect.minX + (btnW - allowStr.size().width) / 2.0, y: allowRect.minY + 3.0, width: btnW, height: 16.0))
            denyStr.draw(in: NSRect(x: denyRect.minX + (btnW - denyStr.size().width) / 2.0, y: denyRect.minY + 3.0, width: btnW, height: 16.0))
            approvalHitRects.append((allowRect, "allow"))
            approvalHitRects.append((denyRect, "deny"))
            return
        }

        if ctrl.isUpdatingSelf || !ctrl.availableUpdateVersion.isEmpty {
            let isEn = I18n.effectiveLanguage(config: ctrl.config) == "en"
            let label = ctrl.isUpdatingSelf && !ctrl.selfUpdateStatusMsg.isEmpty
                ? ctrl.selfUpdateStatusMsg
                : (isEn ? "New release v\(ctrl.availableUpdateVersion) · Click to update" : "发现新版本 v\(ctrl.availableUpdateVersion) · 点击一键更新")
            let font = NSFont.monospacedSystemFont(ofSize: 11.0, weight: .bold)
            let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: NSColor.white]
            let attrStr = NSAttributedString(string: label, attributes: attrs)
            let textSize = attrStr.size()
            let padX: CGFloat = 12.0
            let padY: CGFloat = 6.0
            let bw = min(bounds.width - 12.0, max(150.0, textSize.width + padX * 2))
            let bh = max(28.0, ceil(textSize.height) + padY * 2)
            let bx = (bounds.width - bw) / 2.0
            let by = max(8.0, spriteY - bh - 10.0)
            let rect = NSRect(x: bx, y: by, width: bw, height: bh)
            drawPixelBox(rect: rect, fill: NSColor(calibratedRed: 0.18, green: 0.06, blue: 0.40, alpha: 0.96), border: NSColor(calibratedRed: 0.66, green: 0.33, blue: 0.97, alpha: 1.0), lineWidth: 2.0)
            attrStr.draw(in: NSRect(x: bx + padX, y: by + (bh - textSize.height) / 2.0, width: bw - padX * 2, height: textSize.height + 2.0))
            bubbleHitRects.append((rect, "__self_update__"))
            return
        }

        if ctrl.config.BubbleMode == "off" { return }

        if !ctrl.dockSide.isEmpty && !ctrl.dockPeekOut && !ctrl.fullscreenPeekActive && !ctrl.bubbleExpanded {
            let isAlertState = (effState == "needs_input" || effState == "ready" || effState == "blocked")
            if effState != "idle" && (ctrl.config.BubbleMode != "alerts" || isAlertState) {
                let bw: CGFloat = 34.0 * scale
                let bh: CGFloat = 14.0 * scale
                let bx = (ctrl.dockSide == "right") ? (bounds.width / 2.0 - bw - 6.0 * scale) : (bounds.width / 2.0 + 6.0 * scale)
                let by = max(8.0, spriteY - bh - 10.0)
                let rect = NSRect(x: bx, y: by, width: bw, height: bh)
                drawPixelBox(rect: rect, fill: ctrl.accentColor(for: effState), border: NSColor(calibratedRed: 0.06, green: 0.09, blue: 0.16, alpha: 1.0), lineWidth: 2.0)
            }
            return
        }

        if ctrl.isDetailedStyle() {
            let tasks = ctrl.orderedVisibleTasks()
            if tasks.isEmpty { return }
            let font = NSFont.monospacedSystemFont(ofSize: 11.0, weight: .bold)

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
                    drawPixelBox(rect: rect, fill: NSColor(calibratedRed: 0.08, green: 0.10, blue: 0.16, alpha: 0.96), border: accent, lineWidth: isPinned ? 2.0 : 1.5)

                    let dotRect = NSRect(x: bx + 9.0, y: by + (bh - 6.0) / 2.0, width: 6.0, height: 6.0)
                    accent.setFill()
                    NSBezierPath(rect: dotRect).fill()

                    let tx = bx + 21.0
                    let ty = by + (bh - textSize.height) / 2.0
                    attrStr.draw(in: NSRect(x: tx, y: ty, width: bw - 28.0, height: textSize.height))
                    bubbleHitRects.append((rect, t.conversationId))
                    curBottom = by - 5.0
                }
                return
            }

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
                let b2 = NSRect(x: bx + 14.0, y: by - 12.0, width: max(60.0, bw - 28.0), height: bh)
                drawPixelBox(rect: b2, fill: NSColor(calibratedRed: 0.12, green: 0.16, blue: 0.24, alpha: 0.90), border: ctrl.accentColor(for: tasks[2].state), lineWidth: 1.5)
            }
            if tasks.count >= 2 {
                let b1 = NSRect(x: bx + 7.0, y: by - 6.0, width: max(68.0, bw - 14.0), height: bh)
                drawPixelBox(rect: b1, fill: NSColor(calibratedRed: 0.10, green: 0.14, blue: 0.22, alpha: 0.94), border: ctrl.accentColor(for: tasks[1].state), lineWidth: 1.5)
            }

            let bubbleRect = NSRect(x: bx, y: by, width: bw, height: bh)
            drawPixelBox(rect: bubbleRect, fill: NSColor(calibratedRed: 0.06, green: 0.09, blue: 0.16, alpha: 0.96), border: accent, lineWidth: isPinned ? 2.2 : 2.0)

            let tx = bx + (bw - min(textSize.width, bw - padX * 2)) / 2.0
            let ty = by + (bh - textSize.height) / 2.0
            attrStr.draw(in: NSRect(x: tx, y: ty, width: bw - padX * 2, height: textSize.height))
            bubbleHitRects.append((bubbleRect, top.conversationId))
            return
        }

        if let bubbleText = ctrl.currentBubbleText(), !bubbleText.isEmpty {
            let accent = ctrl.accentColor(for: effState)
            let font = NSFont.monospacedSystemFont(ofSize: 12.0, weight: .bold)
            let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: NSColor.white]
            let attrStr = NSAttributedString(string: bubbleText, attributes: attrs)
            let textSize = attrStr.size()
            let padX: CGFloat = 12.0
            let padY: CGFloat = 7.0
            let bw = min(bounds.width - 12.0, max(54.0, textSize.width + padX * 2.0))
            let bh = textSize.height + padY * 2.0
            let bx = (bounds.width - bw) / 2.0
            let by = max(6.0, spriteY - bh - 12.0)

            let bubbleRect = NSRect(x: bx, y: by, width: bw, height: bh)
            drawPixelBox(rect: bubbleRect, fill: NSColor(calibratedRed: 0.06, green: 0.09, blue: 0.16, alpha: 0.96), border: accent, lineWidth: 2.0)

            // 像素方格小三角
            let midX = floor(bounds.width / 2.0)
            NSColor(calibratedRed: 0.06, green: 0.09, blue: 0.16, alpha: 0.96).setFill()
            NSBezierPath(rect: NSRect(x: midX - 4, y: by + bh - 1, width: 8, height: 4)).fill()
            accent.setFill()
            NSBezierPath(rect: NSRect(x: midX - 2, y: by + bh + 3, width: 4, height: 3)).fill()

            let tx = bx + (bw - min(textSize.width, bw - padX * 2)) / 2.0
            let ty = by + (bh - textSize.height) / 2.0
            attrStr.draw(in: NSRect(x: tx, y: ty, width: bw - padX * 2, height: textSize.height))
        }
    }

    private func drawPixelBox(rect: NSRect, fill: NSColor, border: NSColor, lineWidth: CGFloat) {
        NSColor(calibratedWhite: 0.0, alpha: 0.55).setFill()
        NSBezierPath(rect: NSRect(x: rect.minX + 3.0, y: rect.minY + 3.0, width: rect.width, height: rect.height)).fill()
        let path = NSBezierPath(roundedRect: rect, xRadius: 2.0, yRadius: 2.0)
        fill.setFill()
        path.fill()
        border.setStroke()
        path.lineWidth = lineWidth
        path.stroke()
    }

    private func getFrameMotionOffset(row: Int, frame: Int) -> (Int, Int, Bool) {
        switch row {
        case 0:
            return (0, (frame == 2 || frame == 3) ? 1 : 0, false)
        case 1:
            return (0, ((frame % 4) == 1 || (frame % 4) == 3) ? -1 : 0, false)
        case 2:
            return (0, ((frame % 4) == 1 || (frame % 4) == 3) ? -1 : 0, true)
        case 3:
            return (0, (frame % 2) == 1 ? -1 : 0, false)
        case 4:
            let jOff = [1, -2, -5, -2, 0]
            return (0, jOff[frame % jOff.count], false)
        case 5:
            return ((frame % 2) == 0 ? -1 : 1, 0, false)
        case 6:
            return (0, (frame % 2) == 1 ? -1 : 0, false)
        case 7:
            let wOff = [0, 1, 0, -1, 0, 1]
            return (0, wOff[frame % wOff.count], false)
        case 8:
            let rOff = [0, -2, -4, -1, 0, -2]
            return (0, rOff[frame % rOff.count], false)
        default:
            return (0, 0, false)
        }
    }

    private func drawPixelAccessory(ctx: CGContext, accessory: String, skinId: String, row: Int, frame: Int, destRect: NSRect) {
        let gw = 36
        let gh = 39
        var grid = [[NSColor?]](repeating: [NSColor?](repeating: nil, count: gw), count: gh)
        func px(_ x: Int, _ y: Int, _ color: NSColor) {
            if x >= 0 && x < gw && y >= 0 && y < gh { grid[y][x] = color }
        }

        let (bodyX, bodyY, flipX) = getFrameMotionOffset(row: row, frame: frame)
        let cx = 18 + bodyX
        let cy = 22 + bodyY
        let ey = cy - 7
        let headOffset = skinId == "flutter-dash" ? -1 : (skinId == "android-bugdroid" ? 1 : 0)
        let headY = cy - 11 + headOffset

        switch accessory {
        case "glasses":
            let frameC = NSColor(calibratedRed: 0.06, green: 0.09, blue: 0.16, alpha: 1.0)
            let templeC = NSColor(calibratedRed: 0.12, green: 0.16, blue: 0.23, alpha: 1.0)
            let lensC = NSColor(calibratedRed: 0.12, green: 0.16, blue: 0.23, alpha: 0.94)
            let glint = NSColor(calibratedRed: 0.22, green: 0.74, blue: 0.97, alpha: 1.0)
            let glintHi = NSColor(calibratedRed: 0.88, green: 0.95, blue: 1.0, alpha: 1.0)
            let gy = ey + 1
            px(cx - 8, gy, templeC); px(cx + 7, gy, templeC)
            for x in (cx - 2)...(cx + 1) { px(x, gy, frameC) }
            for x in (cx - 7)...(cx - 2) { px(x, gy, frameC) }
            for x in (cx - 6)...(cx - 3) { for y in (gy + 1)...(gy + 2) { px(x, y, lensC) } }
            px(cx - 6, gy + 1, glint); px(cx - 5, gy + 1, glintHi); px(cx - 5, gy + 2, glint)
            for x in (cx + 1)...(cx + 6) { px(x, gy, frameC) }
            for x in (cx + 2)...(cx + 5) { for y in (gy + 1)...(gy + 2) { px(x, y, lensC) } }
            px(cx + 2, gy + 1, glint); px(cx + 3, gy + 1, glintHi); px(cx + 3, gy + 2, glint)
        case "cap":
            let capHi = NSColor(calibratedRed: 0.22, green: 0.74, blue: 0.97, alpha: 1.0)
            let capMid = NSColor(calibratedRed: 0.05, green: 0.65, blue: 0.91, alpha: 1.0)
            let capBlue = NSColor(calibratedRed: 0.01, green: 0.52, blue: 0.78, alpha: 1.0)
            let capDark = NSColor(calibratedRed: 0.01, green: 0.41, blue: 0.63, alpha: 1.0)
            let capDeep = NSColor(calibratedRed: 0.03, green: 0.35, blue: 0.52, alpha: 1.0)
            let badgeW = NSColor(calibratedRed: 0.97, green: 0.98, blue: 0.99, alpha: 1.0)
            let badgeG = NSColor(calibratedRed: 0.98, green: 0.80, blue: 0.08, alpha: 1.0)
            for x in (cx - 4)...(cx + 3) { px(x, headY - 4, capHi) }
            for x in (cx - 6)...(cx + 5) { px(x, headY - 3, capMid) }
            for x in (cx - 7)...(cx + 5) { for y in (headY - 2)...(headY - 1) { px(x, y, capBlue) } }
            px(cx - 1, headY - 2, badgeW); px(cx, headY - 2, badgeG)
            for x in (cx - 5)...(cx + 8) { px(x, headY, capDark) }
            for x in (cx + 2)...(cx + 9) { px(x, headY + 1, capDeep) }
        case "crown":
            let gold = NSColor(calibratedRed: 0.98, green: 0.80, blue: 0.08, alpha: 1.0)
            let goldHi = NSColor(calibratedRed: 0.99, green: 0.88, blue: 0.28, alpha: 1.0)
            let pearl = NSColor(calibratedRed: 1.0, green: 0.98, blue: 0.76, alpha: 1.0)
            let amber = NSColor(calibratedRed: 0.85, green: 0.47, blue: 0.02, alpha: 1.0)
            let gem = NSColor(calibratedRed: 0.22, green: 0.74, blue: 0.97, alpha: 1.0)
            let gemHi = NSColor(calibratedRed: 0.73, green: 0.90, blue: 0.99, alpha: 1.0)
            let ruby = NSColor(calibratedRed: 0.96, green: 0.25, blue: 0.37, alpha: 1.0)
            for x in (cx - 5)...(cx + 4) { px(x, headY - 1, gold); px(x, headY, amber) }
            px(cx - 5, headY - 2, gold); px(cx - 5, headY - 3, goldHi); px(cx - 5, headY - 4, pearl)
            px(cx + 4, headY - 2, gold); px(cx + 4, headY - 3, goldHi); px(cx + 4, headY - 4, pearl)
            for x in (cx - 2)...(cx + 1) { px(x, headY - 2, gold) }
            px(cx - 1, headY - 3, goldHi); px(cx, headY - 3, goldHi)
            px(cx - 1, headY - 4, goldHi); px(cx, headY - 4, goldHi)
            px(cx - 1, headY - 5, pearl); px(cx, headY - 5, pearl)
            px(cx - 1, headY - 2, gem); px(cx, headY - 2, gemHi)
            px(cx - 4, headY - 1, ruby); px(cx + 3, headY - 1, ruby)
        case "coffee":
            let cup = NSColor(calibratedRed: 0.97, green: 0.98, blue: 0.99, alpha: 1.0)
            let cupShad = NSColor(calibratedRed: 0.80, green: 0.84, blue: 0.88, alpha: 1.0)
            let cupRim = NSColor(calibratedRed: 0.89, green: 0.91, blue: 0.94, alpha: 1.0)
            let coffee = NSColor(calibratedRed: 0.47, green: 0.21, blue: 0.06, alpha: 1.0)
            let crema = NSColor(calibratedRed: 0.57, green: 0.25, blue: 0.05, alpha: 1.0)
            let sleeve = NSColor(calibratedRed: 0.85, green: 0.47, blue: 0.02, alpha: 1.0)
            let steam = NSColor(calibratedRed: 0.89, green: 0.91, blue: 0.94, alpha: 0.86)
            let steamFade = NSColor(calibratedRed: 0.80, green: 0.84, blue: 0.88, alpha: 0.66)
            let cupX = cx + 8
            let cupY = cy + 1
            for x in cupX...(cupX + 3) { for y in (cupY + 1)...(cupY + 5) { px(x, y, cup) } }
            for y in (cupY + 1)...(cupY + 5) { px(cupX + 4, y, cupShad) }
            px(cupX, cupY, cup); px(cupX + 4, cupY, cupRim)
            px(cupX + 1, cupY, coffee); px(cupX + 2, cupY, crema); px(cupX + 3, cupY, coffee)
            for x in (cupX + 1)...(cupX + 3) { px(x, cupY + 3, sleeve) }
            px(cupX + 5, cupY + 1, cupRim); px(cupX + 6, cupY + 2, cupRim)
            px(cupX + 6, cupY + 3, cupShad); px(cupX + 5, cupY + 4, cupShad)
            let steamOff = frame % 2
            px(cupX + 1, cupY - 2 - steamOff, steam); px(cupX + 2, cupY - 3 - steamOff, steamFade)
            px(cupX + 3, cupY - 2 + steamOff, steam); px(cupX + 4, cupY - 3 + steamOff, steamFade)
        default:
            return
        }

        // 添加 1px 深色轮廓像素
        let outline = NSColor(calibratedRed: 0.06, green: 0.09, blue: 0.16, alpha: 1.0)
        var finalGrid = grid
        let dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)]
        for y in 0..<gh {
            for x in 0..<gw {
                if grid[y][x] != nil { continue }
                for (dx, dy) in dirs {
                    let nx = x + dx, ny = y + dy
                    if nx >= 0 && nx < gw && ny >= 0 && ny < gh, let nc = grid[ny][nx], nc.alphaComponent >= 0.85 {
                        finalGrid[y][x] = outline
                        break
                    }
                }
            }
        }

        let sx = destRect.width / 192.0
        let sy = destRect.height / 208.0
        let ox = destRect.minX
        let oy = destRect.minY
        for y in 0..<gh {
            for x in 0..<gw {
                guard let c = finalGrid[y][x] else { continue }
                let drawX = flipX ? (gw - 1 - x) : x
                let rx = ox + (6.0 + CGFloat(drawX) * 5.0) * sx
                let ry = oy + (6.0 + CGFloat(y) * 5.0) * sy
                c.setFill()
                ctx.fill(CGRect(x: rx, y: ry, width: 5.0 * sx, height: 5.0 * sy))
            }
        }
    }

    // 鼠标拖拽、气泡悬停展开与点击跳转
    private var dragStartScreen: NSPoint = .zero
    private var lastDragScreenX: CGFloat = 0
    private var windowStartOrigin: NSPoint = .zero
    private var didDrag: Bool = false
    private var bubbleHitRects: [(NSRect, String)] = []
    private var approvalHitRects: [(NSRect, String)] = []
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

    private var pendingClickWork: DispatchWorkItem?

    override func mouseUp(with event: NSEvent) {
        controller?.isDragging = false
        controller?.dragTargetAngle = 0
        if didDrag {
            pendingClickWork?.cancel()
            pendingClickWork = nil
            controller?.snapToScreenEdgeIfNear()
            controller?.saveWindowPosition()
            controller?.overrideRow = PetAnimRow.jump.rawValue
            controller?.overrideUntil = Date().addingTimeInterval(0.6)
        } else {
            let pt = convert(event.locationInWindow, from: nil)
            if let appHit = approvalHitRects.first(where: { $0.0.contains(pt) }) {
                pendingClickWork?.cancel()
                pendingClickWork = nil
                controller?.resolveCurrentApproval(decision: appHit.1)
            } else if let hit = bubbleHitRects.first(where: { $0.0.contains(pt) }) {
                pendingClickWork?.cancel()
                pendingClickWork = nil
                if hit.1 == "__self_update__" {
                    controller?.performSelfUpdate()
                } else {
                    controller?.openConversation(hit.1)
                }
            } else if event.clickCount >= 2 {
                pendingClickWork?.cancel()
                pendingClickWork = nil
                controller?.showQuickDispatchWindow()
            } else {
                pendingClickWork?.cancel()
                let work = DispatchWorkItem { [weak self] in
                    self?.controller?.handleLeftClick()
                }
                pendingClickWork = work
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.24, execute: work)
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
    var currentApproval: PendingApproval?
    var approvalQueue: [PendingApproval] = []
    var hiddenByFullscreen: Bool = false
    var fullscreenPeekActive: Bool = false
    var savedNormalOrigin: NSPoint? = nil
    var dockSide: String = ""
    var dockPeekOut: Bool = false
    var dockHoverLastSeen: Date = .distantPast
    var dockIgnoreHoverUntil: Date = .distantPast

    let port: UInt16 = 47821
    var serverSocket: Int32 = -1
    var hotKeyRef: EventHotKeyRef?
    var httpBridgeToken: String = ""
    var availableUpdateVersion: String = ""
    var availableUpdateZipUrl: String = ""
    var updatePromptShownAt: Date = Date.distantPast
    var isUpdatingSelf: Bool = false
    var selfUpdateStatusMsg: String = ""

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
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
            self?.checkGitHubReleaseIfNeeded()
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
        if !config.Scale.isFinite || config.Scale < 0.33 || config.Scale > 3.0 { config.Scale = 1.0 }
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
            let isCustomDir = (dir == userPetsDir)
            guard let items = try? FileManager.default.contentsOfDirectory(at: dir, includingPropertiesForKeys: nil) else { continue }
            for item in items {
                let manifest = item.appendingPathComponent("pet.json")
                guard let data = try? Data(contentsOf: manifest),
                      let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { continue }
                let id = (obj["id"] as? String) ?? item.lastPathComponent
                let dn = (obj["displayName"] as? String) ?? id
                let desc = (obj["description"] as? String) ?? ""
                let ver = (obj["spriteVersionNumber"] as? Int) ?? 2
                let themeHex = (obj["themeColor"] as? String) ?? "#38BDF8"
                var soundMap: [String: String] = [:]
                if let rawSounds = obj["sounds"] as? [String: String] {
                    for (k, v) in rawSounds {
                        let p = item.appendingPathComponent(v).path
                        if FileManager.default.fileExists(atPath: p) { soundMap[k.lowercased()] = p }
                    }
                }
                let rel = (obj["spritesheetPath"] as? String) ?? "spritesheet.png"
                let sheetPath = item.appendingPathComponent(rel).path
                let img = NSImage(contentsOfFile: sheetPath)
                var frameMap: [Int: [CGImage]] = [:]
                if img == nil {
                    let actionDirs: [(Int, String)] = [
                        (0, "idle"), (1, "walk_right"), (2, "walk_left"), (3, "wave"),
                        (4, "jump"), (5, "blocked"), (6, "needs_input"), (7, "running"), (8, "ready")
                    ]
                    for (rowIdx, dirName) in actionDirs {
                        let sub = item.appendingPathComponent(dirName)
                        if let files = try? FileManager.default.contentsOfDirectory(at: sub, includingPropertiesForKeys: nil) {
                            let pngs = files.filter { $0.pathExtension.lowercased() == "png" }.sorted { $0.lastPathComponent < $1.lastPathComponent }
                            var cgs: [CGImage] = []
                            for f in pngs {
                                if let nsF = NSImage(contentsOf: f), let cgF = nsF.cgImage(forProposedRect: nil, context: nil, hints: nil) {
                                    cgs.append(cgF)
                                }
                            }
                            if !cgs.isEmpty { frameMap[rowIdx] = cgs }
                        }
                    }
                }
                if img != nil || !frameMap.isEmpty {
                    packs[id] = PetPack(id: id, displayName: dn, description: desc, version: ver, spritesheetPath: sheetPath, image: img, themeColorHex: themeHex, sounds: soundMap, frameMap: frameMap, isCustom: isCustomDir)
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
        let clamped = min(3.0, max(0.33, newScale))
        let s = (clamped * 100.0).rounded() / 100.0
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
        overrideRow = Bool.random() ? PetAnimRow.jump.rawValue : PetAnimRow.wave.rawValue
        overrideUntil = Date().addingTimeInterval(0.9)
        canvasView.needsDisplay = true
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

    func syncWindowSizeForBubbles() {
        let baseSz = windowSizeForScale(config.Scale)
        if !isDetailedStyle() || config.BubbleMode == "off" {
            if abs(window.frame.width - baseSz.width) > 0.5 || abs(window.frame.height - baseSz.height) > 0.5 {
                var f = window.frame
                let cx = f.midX
                f.size = baseSz
                f.origin.x = cx - f.size.width / 2.0
                window.setFrame(f, display: true)
                canvasView.frame = NSRect(origin: .zero, size: f.size)
            }
            return
        }
        let tasks = orderedVisibleTasks()
        let font = NSFont.systemFont(ofSize: 11.5, weight: .bold)
        var maxTextW: CGFloat = 100.0
        let inspectCount = bubbleExpanded ? min(5, tasks.count) : min(1, tasks.count)
        for i in 0..<inspectCount {
            var lbl = formatDetailedTaskLabel(tasks[i])
            if !bubbleExpanded && tasks.count > 1 { lbl += " (+\(tasks.count - 1))" }
            let sz = NSAttributedString(string: lbl, attributes: [.font: font]).size()
            if sz.width > maxTextW { maxTextW = sz.width }
        }
        let maxWinW = CGFloat(390.0 * config.Scale)
        let targetW = min(maxWinW, max(baseSz.width, ceil(maxTextW + 58.0)))
        let showCount = bubbleExpanded ? min(5, tasks.count) : 1
        let extraH = bubbleExpanded && showCount > 1 ? CGFloat(showCount - 1) * 31.0 : (tasks.count >= 3 ? 12.0 : (tasks.count >= 2 ? 6.0 : 0.0))
        let targetH = baseSz.height + extraH
        if abs(window.frame.width - targetW) > 0.5 || abs(window.frame.height - targetH) > 0.5 {
            var f = window.frame
            let cx = f.midX
            f.size = NSSize(width: targetW, height: targetH)
            f.origin.x = cx - targetW / 2.0
            window.setFrame(f, display: true)
            canvasView.frame = NSRect(origin: .zero, size: f.size)
        }
    }

    func setBubbleExpanded(_ expand: Bool) {
        if bubbleExpanded == expand { return }
        bubbleExpanded = expand
        syncWindowSizeForBubbles()
        canvasView.needsDisplay = true
    }

    func orderedVisibleTasks() -> [ConvState] {
        let now = Date()
        var list: [ConvState] = []
        for (cid, var cs) in conversations {
            let age = now.timeIntervalSince(cs.updatedAt)
            if cs.state == "running" && age > 45 { cs.state = "idle"; conversations[cid] = cs }
            if cs.state == "ready" && config.ReadyTimeoutSec > 0 && age >= Double(config.ReadyTimeoutSec) { cs.state = "idle"; conversations[cid] = cs }
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

    func shortDetailedState(_ cs: ConvState, lang: String) -> String {
        let m = cs.message.trimmingCharacters(in: .whitespacesAndNewlines)
        let isEn = (lang == "en")
        if m == "思考规划中..." || m == "思考执行中..." { return isEn ? "Thinking" : "思考中" }
        if m == "编辑代码中..." || m == "修改代码中..." || m == "快速分析代码中..." { return isEn ? "Editing" : "改代码" }
        if m == "读取文件中..." { return isEn ? "Reading" : "读文件" }
        if m == "编写文件中..." { return isEn ? "Writing" : "写文件" }
        if m == "检索代码中..." { return isEn ? "Searching" : "搜代码" }
        if m == "执行命令中..." { return isEn ? "Command" : "跑命令" }
        if m == "运行检查中..." { return isEn ? "Checks" : "检查中" }
        if m == "处理结果中..." { return isEn ? "Finishing" : "收尾中" }
        if m == "查看屏幕中..." || m == "操作桌面应用中..." || m == "操控桌面中..." { return isEn ? "Screen" : "看屏幕" }
        if m == "任务完成!" { return isEn ? "Ready" : "已完成" }
        if m.contains("出错") { return isEn ? "Blocked" : "报错" }
        switch cs.state {
        case "needs_input": return isEn ? "Input" : "待确认"
        case "blocked": return isEn ? "Blocked" : "报错"
        case "ready": return isEn ? "Ready" : "已完成"
        case "running": return isEn ? "Running" : "运行中"
        default: return isEn ? "Idle" : "空闲"
        }
    }

    func formatDetailedTaskLabel(_ cs: ConvState) -> String {
        let lang = I18n.effectiveLanguage(config: config)
        let rawTitle = cs.title.trimmingCharacters(in: .whitespacesAndNewlines)
        let taskName: String
        if !rawTitle.isEmpty {
            taskName = rawTitle.count > 14 ? String(rawTitle.prefix(14)) + "…" : rawTitle
        } else if cs.conversationId == "default" || cs.conversationId.isEmpty {
            taskName = "Antigravity"
        } else {
            taskName = String(cs.conversationId.prefix(8))
        }
        return "\(taskName) - \(shortDetailedState(cs, lang: lang))"
    }

    func currentTopConversation() -> ConvState? {
        let now = Date()
        if !config.PinnedConversationId.isEmpty, var pinned = conversations[config.PinnedConversationId] {
            let age = now.timeIntervalSince(pinned.updatedAt)
            if pinned.state == "running" && age > 45 { pinned.state = "idle"; conversations[config.PinnedConversationId] = pinned }
            if pinned.state == "ready" && config.ReadyTimeoutSec > 0 && age >= Double(config.ReadyTimeoutSec) { pinned.state = "idle"; conversations[config.PinnedConversationId] = pinned }
            if pinned.state != "idle" { return pinned }
        }
        var best: ConvState?
        for (cid, var cs) in conversations {
            let age = now.timeIntervalSince(cs.updatedAt)
            if cs.state == "running" && age > 45 { cs.state = "idle"; conversations[cid] = cs }
            if cs.state == "ready" && config.ReadyTimeoutSec > 0 && age >= Double(config.ReadyTimeoutSec) { cs.state = "idle"; conversations[cid] = cs }
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

    func parseHexColor(_ hex: String) -> NSColor? {
        var s = hex.trimmingCharacters(in: .whitespacesAndNewlines)
        if s.hasPrefix("#") { s.removeFirst() }
        guard s.count == 6, let val = Int(s, radix: 16) else { return nil }
        let r = CGFloat((val >> 16) & 0xFF) / 255.0
        let g = CGFloat((val >> 8) & 0xFF) / 255.0
        let b = CGFloat(val & 0xFF) / 255.0
        return NSColor(calibratedRed: r, green: g, blue: b, alpha: 1.0)
    }

    func accentColor(for state: String) -> NSColor {
        switch state {
        case "needs_input": return NSColor(calibratedRed: 1.0, green: 0.70, blue: 0.0, alpha: 1.0)
        case "blocked": return NSColor(calibratedRed: 1.0, green: 0.35, blue: 0.37, alpha: 1.0)
        case "ready": return NSColor(calibratedRed: 0.30, green: 0.85, blue: 0.39, alpha: 1.0)
        case "running":
            if let pack = activePack(), let c = parseHexColor(pack.themeColorHex) { return c }
            return NSColor(calibratedRed: 0.31, green: 0.76, blue: 0.97, alpha: 1.0)
        default:
            if let pack = activePack(), let c = parseHexColor(pack.themeColorHex) { return c }
            return NSColor(calibratedRed: 0.65, green: 0.55, blue: 0.98, alpha: 1.0)
        }
    }

    func accessoryUnlockMilestone(_ acc: String) -> Int {
        switch acc.lowercased() {
        case "glasses": return 20
        case "cap": return 50
        case "coffee": return 100
        case "crown": return 200
        default: return 0
        }
    }

    func isAccessoryUnlocked(_ acc: String) -> Bool {
        let mode = acc.lowercased()
        if mode == "auto" || mode == "none" || mode.isEmpty { return true }
        let req = accessoryUnlockMilestone(mode)
        return req > 0 && config.CompletedTasksCount >= req
    }

    func isAccessorySupportedForCurrentSkin() -> Bool {
        return !(activePack()?.isCustom ?? false)
    }

    func effectiveAccessory() -> String {
        if !isAccessorySupportedForCurrentSkin() { return "none" }
        let mode = config.Accessory.lowercased()
        if mode == "none" { return "none" }
        if mode == "glasses" || mode == "cap" || mode == "coffee" || mode == "crown" {
            return isAccessoryUnlocked(mode) ? mode : "none"
        }
        let c = config.CompletedTasksCount
        if c >= 200 { return "crown" }
        if c >= 100 { return "coffee" }
        if c >= 50 { return "cap" }
        if c >= 20 { return "glasses" }
        return "none"
    }

    func playPetSound(_ stateKey: String) {
        if let pack = activePack(), let path = pack.sounds[stateKey.lowercased()], FileManager.default.fileExists(atPath: path) {
            NSSound(contentsOfFile: path, byReference: true)?.play()
            return
        }
        let sName = stateKey == "blocked" ? "Basso" : (stateKey == "needs_input" ? "Ping" : "Glass")
        NSSound(named: NSSound.Name(sName))?.play()
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

        if !availableUpdateVersion.isEmpty && !isUpdatingSelf && config.ReadyTimeoutSec > 0 {
            if now.timeIntervalSince(updatePromptShownAt) >= Double(config.ReadyTimeoutSec) {
                availableUpdateVersion = ""
                availableUpdateZipUrl = ""
                canvasView.needsDisplay = true
                syncWindowSizeForBubbles()
            }
        }

        let st = currentEffectiveState()
        let urgentState = (st == "ready" || st == "needs_input" || st == "blocked" || currentApproval != nil || !availableUpdateVersion.isEmpty || isUpdatingSelf)
        var restAngle: CGFloat = 0.0
        if !isDragging && !dockSide.isEmpty && !fullscreenPeekActive {
            let hoverAllowed = now >= dockIgnoreHoverUntil
            let activeHover = (hoverAllowed && isCursorInDockedZone(side: dockSide)) || bubbleExpanded || urgentState
            if activeHover {
                dockPeekOut = true
                dockHoverLastSeen = now
            } else if dockPeekOut && now.timeIntervalSince(dockHoverLastSeen) > 0.26 {
                dockPeekOut = false
            }
            let targetX = dockedTargetX(side: dockSide, peekOut: dockPeekOut)
            if abs(window.frame.origin.x - targetX) > 0.8 {
                let nx = window.frame.origin.x + (targetX - window.frame.origin.x) * 0.25
                window.setFrameOrigin(NSPoint(x: nx, y: window.frame.origin.y))
            }
            restAngle = dockPeekOut ? 0.0 : (dockSide == "right" ? -14.0 : 14.0)
        }

        if isDragging {
            let dangle = CGFloat(sin(animTime * 7.0) * 3.5)
            let target = dragTargetAngle + dangle
            dragCurrentAngle += (target - dragCurrentAngle) * 0.35
            dragTargetAngle *= 0.78
            dragScaleX += (0.94 - dragScaleX) * 0.35
            dragScaleY += (1.07 - dragScaleY) * 0.35
        } else if abs(dragCurrentAngle - restAngle) > 0.1 || abs(dragScaleX - 1.0) > 0.005 {
            dragCurrentAngle += (restAngle - dragCurrentAngle) * 0.32
            if abs(dragCurrentAngle - restAngle) < 0.1 { dragCurrentAngle = restAngle }
            dragScaleX += (1.0 - dragScaleX) * 0.35
            dragScaleY += (1.0 - dragScaleY) * 0.35
        }

        if st == "idle" {
            // 待机散步
            if config.AutoWalk && !fullscreenPeekActive && !hiddenByFullscreen && dockSide.isEmpty {
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
            // 全屏应用避让、关键提醒贴边出现与 Antigravity 窗口边缘吸附
            if config.HideOnFullscreen {
                let fs = isOtherAppFullscreen()
                let urgent = (st == "ready" || st == "needs_input" || st == "blocked" || currentApproval != nil)
                if fs {
                    if urgent {
                        if !fullscreenPeekActive {
                            savedNormalOrigin = window.frame.origin
                            fullscreenPeekActive = true
                            hiddenByFullscreen = false
                        }
                        if let screen = NSScreen.main?.visibleFrame {
                            let peekX = (dockSide == "left") ? (screen.minX - 6.0) : (screen.maxX - window.frame.width + 6.0)
                            let peekY = max(screen.minY + 24.0, min(screen.maxY - window.frame.height - 40.0, window.frame.origin.y))
                            if abs(window.frame.origin.x - peekX) > 1.0 || abs(window.frame.origin.y - peekY) > 1.0 {
                                window.setFrameOrigin(NSPoint(x: peekX, y: peekY))
                            }
                        }
                        window.alphaValue = 1.0
                        window.orderFrontRegardless()
                    } else {
                        fullscreenPeekActive = false
                        hiddenByFullscreen = true
                        window.alphaValue = 0.0
                    }
                } else {
                    if fullscreenPeekActive {
                        if let saved = savedNormalOrigin { window.setFrameOrigin(saved) }
                        fullscreenPeekActive = false
                    }
                    if hiddenByFullscreen {
                        hiddenByFullscreen = false
                    }
                    window.alphaValue = 1.0
                }
            } else {
                if fullscreenPeekActive {
                    if let saved = savedNormalOrigin { window.setFrameOrigin(saved) }
                    fullscreenPeekActive = false
                }
                if hiddenByFullscreen {
                    hiddenByFullscreen = false
                }
                window.alphaValue = 1.0
            }

            if config.SnapToAgy && !isDragging && walkDir == 0 && !hiddenByFullscreen && !fullscreenPeekActive && dockSide.isEmpty {
                if let agyRect = findAntigravityWindowRect() {
                    let targetX = agyRect.maxX - window.frame.width - 18.0
                    let targetY = agyRect.maxY - window.frame.height + 6.0
                    let dx = targetX - window.frame.origin.x
                    let dy = targetY - window.frame.origin.y
                    if abs(dx) > 2.0 || abs(dy) > 2.0 {
                        window.setFrameOrigin(NSPoint(x: window.frame.origin.x + dx * 0.25, y: window.frame.origin.y + dy * 0.25))
                    }
                }
            }

            canvasView.needsDisplay = true
        }
    }

    func activeScreenVisibleFrame() -> NSRect? {
        return window.screen?.visibleFrame ?? NSScreen.main?.visibleFrame
    }

    func isCursorInDockedZone(side: String) -> Bool {
        guard let screen = activeScreenVisibleFrame() else { return false }
        let m = NSEvent.mouseLocation
        let minX: CGFloat
        let maxX: CGFloat
        if !dockPeekOut {
            let narrowW = round(68.0 * CGFloat(config.Scale))
            if side == "right" {
                minX = screen.maxX - narrowW
                maxX = screen.maxX + 6.0
            } else {
                minX = screen.minX - 6.0
                maxX = screen.minX + narrowW
            }
        } else {
            if side == "right" {
                minX = min(window.frame.origin.x, dockedTargetX(side: "right", peekOut: true)) - 8.0
                maxX = screen.maxX + 8.0
            } else {
                minX = screen.minX - 8.0
                maxX = max(window.frame.maxX, dockedTargetX(side: "left", peekOut: true) + window.frame.width) + 8.0
            }
        }
        let zone = NSRect(x: minX, y: window.frame.origin.y - 8.0, width: maxX - minX, height: window.frame.height + 16.0)
        return zone.contains(m)
    }

    func dockedTargetX(side: String, peekOut: Bool) -> CGFloat {
        guard let screen = activeScreenVisibleFrame() else { return window.frame.origin.x }
        let bodyHalfW = (140.0 * CGFloat(config.Scale)) * 0.28
        let w = window.frame.width
        if side == "left" {
            let halfPeek = screen.minX - w / 2.0 + bodyHalfW * 0.12
            if peekOut {
                return max(halfPeek, screen.minX - 14.0)
            }
            return halfPeek
        } else {
            let halfPeek = screen.maxX - w / 2.0 - bodyHalfW * 0.12
            if peekOut {
                return min(halfPeek, screen.maxX - w + 14.0)
            }
            return halfPeek
        }
    }

    func snapToScreenEdgeIfNear() {
        guard let screen = activeScreenVisibleFrame() else { return }
        let f = window.frame
        let centerX = f.origin.x + f.width / 2.0
        let bodyHalfW = (140.0 * CGFloat(config.Scale)) * 0.28
        let cursorX = NSEvent.mouseLocation.x
        dockPeekOut = false
        if (centerX - bodyHalfW <= screen.minX + 85.0) || (cursorX <= screen.minX + 60.0) {
            dockSide = "left"
            dockIgnoreHoverUntil = Date().addingTimeInterval(0.7)
            window.setFrameOrigin(NSPoint(x: dockedTargetX(side: "left", peekOut: false), y: f.origin.y))
        } else if (centerX + bodyHalfW >= screen.maxX - 85.0) || (cursorX >= screen.maxX - 60.0) {
            dockSide = "right"
            dockIgnoreHoverUntil = Date().addingTimeInterval(0.7)
            window.setFrameOrigin(NSPoint(x: dockedTargetX(side: "right", peekOut: false), y: f.origin.y))
        } else {
            dockSide = ""
        }
    }

    func isOtherAppFullscreen() -> Bool {
        guard let screen = NSScreen.main?.frame else { return false }
        guard let infoList = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] else { return false }
        let myPid = ProcessInfo.processInfo.processIdentifier
        for info in infoList {
            guard let pid = info[kCGWindowOwnerPID as String] as? pid_t, pid != myPid else { continue }
            guard let layer = info[kCGWindowLayer as String] as? Int, layer == 0 else { continue }
            guard let owner = info[kCGWindowOwnerName as String] as? String else { continue }
            if owner == "Dock" || owner == "Window Server" || owner == "Finder" || owner == "Antigravity" { continue }
            if let boundsDict = info[kCGWindowBounds as String] as? [String: Any],
               let w = boundsDict["Width"] as? CGFloat,
               let h = boundsDict["Height"] as? CGFloat {
                if w >= screen.width - 4 && h >= screen.height - 4 { return true }
            }
        }
        return false
    }

    func findAntigravityWindowRect() -> NSRect? {
        guard let screen = NSScreen.main?.frame else { return nil }
        guard let infoList = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] else { return nil }
        for info in infoList {
            guard let owner = info[kCGWindowOwnerName as String] as? String, owner.contains("Antigravity") else { continue }
            guard let layer = info[kCGWindowLayer as String] as? Int, layer == 0 else { continue }
            if let boundsDict = info[kCGWindowBounds as String] as? [String: Any],
               let x = boundsDict["X"] as? CGFloat,
               let y = boundsDict["Y"] as? CGFloat,
               let w = boundsDict["Width"] as? CGFloat,
               let h = boundsDict["Height"] as? CGFloat, w >= 300 && h >= 200 {
                // CGWindow 坐标原点在左上角，转换为 AppKit 左下角原点
                let appKitY = screen.height - y - h
                return NSRect(x: x, y: appKitY, width: w, height: h)
            }
        }
        return nil
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

        let dispatchItem = NSMenuItem(title: isEn ? "Quick Dispatch..." : "快捷派活...", action: #selector(menuQuickDispatch), keyEquivalent: "")
        dispatchItem.target = self
        menu.addItem(dispatchItem)

        let dockLeftItem = NSMenuItem(title: isEn ? "Dock Left" : "左贴边隐藏", action: #selector(menuDockLeft), keyEquivalent: "")
        dockLeftItem.state = (dockSide == "left") ? .on : .off
        dockLeftItem.target = self
        menu.addItem(dockLeftItem)

        let dockRightItem = NSMenuItem(title: isEn ? "Dock Right" : "右贴边隐藏", action: #selector(menuDockRight), keyEquivalent: "")
        dockRightItem.state = (dockSide == "right") ? .on : .off
        dockRightItem.target = self
        menu.addItem(dockRightItem)

        let settingsItem = NSMenuItem(title: isEn ? "Settings (Antigravity)..." : "设置 (Antigravity 面板)...", action: #selector(menuOpenSettingsHint), keyEquivalent: "")
        settingsItem.target = self
        menu.addItem(settingsItem)

        menu.addItem(.separator())

        let hideItem = NSMenuItem(title: isEn ? "Hide (⌥⌘P)" : "隐藏桌宠 (⌥⌘P)", action: #selector(menuHide), keyEquivalent: "")
        hideItem.target = self
        menu.addItem(hideItem)

        menu.addItem(.separator())

        let quitItem = NSMenuItem(title: isEn ? "Quit Pet" : "退出桌宠", action: #selector(menuQuit), keyEquivalent: "")
        quitItem.target = self
        menu.addItem(quitItem)

        NSMenu.popUpContextMenu(menu, with: event, for: view)
    }

    @objc func menuDockLeft() {
        dockSide = "left"; dockPeekOut = false
        dockIgnoreHoverUntil = Date().addingTimeInterval(0.7)
        window.setFrameOrigin(NSPoint(x: dockedTargetX(side: "left", peekOut: false), y: window.frame.origin.y))
        canvasView.needsDisplay = true
    }

    @objc func menuDockRight() {
        dockSide = "right"; dockPeekOut = false
        dockIgnoreHoverUntil = Date().addingTimeInterval(0.7)
        window.setFrameOrigin(NSPoint(x: dockedTargetX(side: "right", peekOut: false), y: window.frame.origin.y))
        canvasView.needsDisplay = true
    }

    @objc func menuOpenSettingsHint() {
        openConversation("")
    }

    @objc func menuSelectSkin(_ sender: NSMenuItem) {
        if let id = sender.representedObject as? String, packs[id] != nil {
            config.Skin = id
            saveConfig()
            canvasView.needsDisplay = true
        }
    }

    @objc func menuSelectAccessory(_ sender: NSMenuItem) {
        if let k = sender.representedObject as? String, isAccessorySupportedForCurrentSkin(), isAccessoryUnlocked(k) {
            config.Accessory = k
            saveConfig()
            canvasView.needsDisplay = true
        }
    }

    @objc func menuToggleSnap() { config.SnapToAgy.toggle(); saveConfig() }
    @objc func menuToggleFullscreen() { config.HideOnFullscreen.toggle(); saveConfig() }
    @objc func menuQuickDispatch() { showQuickDispatchWindow() }

    @objc func menuOpenChat() {
        let targetId = activeConvId != "default" ? activeConvId : lastConvId
        openConversation(targetId)
    }

    @objc func menuHide() { setPetVisible(false) }
    @objc func menuQuit() { saveConfig(); NSApp.terminate(nil) }

    func resolveCurrentApproval(decision: String) {
        guard let pending = currentApproval else { return }
        pending.decision = decision
        pending.resolved = true
        pending.semaphore.signal()
        currentApproval = approvalQueue.isEmpty ? nil : approvalQueue.removeFirst()
        canvasView.needsDisplay = true
    }

    func showQuickDispatchWindow() {
        let isEn = I18n.effectiveLanguage(config: config) == "en"
        let alert = NSAlert()
        alert.messageText = isEn ? "Quick Dispatch to Antigravity" : "快捷派活给 Antigravity"
        alert.informativeText = isEn ? "Enter prompt or /command:" : "输入指令或 /slash 命令："
        alert.addButton(withTitle: isEn ? "Send" : "发送")
        alert.addButton(withTitle: isEn ? "Cancel" : "取消")
        let input = NSTextField(frame: NSRect(x: 0, y: 0, width: 300, height: 24))
        input.placeholderString = isEn ? "e.g. Run tests and fix errors" : "例如：运行测试并修复报错"
        alert.accessoryView = input
        NSApp.activate(ignoringOtherApps: true)
        if alert.runModal() == .alertFirstButtonReturn {
            let txt = input.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
            if !txt.isEmpty { sendDispatchToPanel(txt) }
        }
    }

    func sendDispatchToPanel(_ message: String) {
        let bridgeUrl = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent(".antigravity-pet")
            .appendingPathComponent("panel-bridge.json")
        guard let data = try? Data(contentsOf: bridgeUrl),
              let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let bPort = obj["port"] as? Int,
              let token = obj["token"] as? String,
              let url = URL(string: "http://127.0.0.1:\(bPort)/api/dispatch") else { return }
        var req = URLRequest(url: url)
        req.httpMethod = "POST"
        req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let targetConv = activeConvId != "default" ? activeConvId : lastConvId
        let payload: [String: Any] = ["token": token, "conversationId": targetConv, "message": message]
        req.httpBody = try? JSONSerialization.data(withJSONObject: payload)
        URLSession.shared.dataTask(with: req).resume()
    }

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
            self.writeHttpBridgeInfo()

            while true {
                let client = Darwin.accept(fd, nil, nil)
                if client < 0 { break }
                DispatchQueue.global(qos: .userInitiated).async {
                    self.handleClient(client)
                }
            }
        }
    }

    func writeHttpBridgeInfo() {
        let token = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
        httpBridgeToken = token
        let bridgeDir = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".antigravity-pet")
        try? FileManager.default.createDirectory(at: bridgeDir, withIntermediateDirectories: true)
        let info: [String: Any] = ["port": Int(port), "token": token]
        if let data = try? JSONSerialization.data(withJSONObject: info) {
            try? data.write(to: bridgeDir.appendingPathComponent("http-bridge.json"))
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
        var headerToken = ""
        for line in lines.dropFirst() {
            let lower = line.lowercased()
            if lower.hasPrefix("content-length:") {
                contentLength = Int(line.dropFirst(15).trimmingCharacters(in: .whitespaces)) ?? 0
            } else if lower.hasPrefix("x-pet-token:") {
                headerToken = String(line.dropFirst(12)).trimmingCharacters(in: .whitespaces)
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

        if path == "/approval" && method == "POST" {
            let bodyToken = (jsonBody["token"] as? String) ?? ""
            let reqToken = !headerToken.isEmpty ? headerToken : bodyToken
            if !httpBridgeToken.isEmpty && reqToken != httpBridgeToken {
                sendHttpResponse(client, status: "403 Forbidden", body: "{\"ok\":false,\"error\":\"forbidden\"}")
                return
            }
            let toolName = (jsonBody["toolName"] as? String) ?? "Tool"
            let summary = (jsonBody["summary"] as? String) ?? ""
            let cid = (jsonBody["conversationId"] as? String) ?? "default"
            let pending = PendingApproval(id: UUID().uuidString, toolName: toolName, summary: summary, conversationId: cid)
            DispatchQueue.main.async {
                if self.currentApproval == nil {
                    self.currentApproval = pending
                } else {
                    self.approvalQueue.append(pending)
                }
                self.playPetSound("needs_input")
                self.canvasView.needsDisplay = true
                self.syncWindowSizeForBubbles()
            }
            let waitRes = pending.semaphore.wait(timeout: .now() + .seconds(45))
            if waitRes == .timedOut {
                DispatchQueue.main.async {
                    if self.currentApproval?.id == pending.id {
                        self.currentApproval = self.approvalQueue.isEmpty ? nil : self.approvalQueue.removeFirst()
                    } else {
                        self.approvalQueue.removeAll { $0.id == pending.id }
                    }
                    self.canvasView.needsDisplay = true
                }
                sendHttpResponse(client, status: "200 OK", body: "{\"ok\":true,\"decision\":\"ask\",\"timeout\":true}")
                return
            }
            let dec = pending.decision == "allow" || pending.decision == "deny" ? pending.decision : "ask"
            sendHttpResponse(client, status: "200 OK", body: "{\"ok\":true,\"decision\":\"\(dec)\"}")
            return
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
                playPetSound(st)
                lastSoundState = st
                lastSoundAt = Date()
            }
            canvasView.needsDisplay = true
            syncWindowSizeForBubbles()
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
            syncWindowSizeForBubbles()
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
            case "readytimeoutsec":
                if let sec = Int(val), sec >= 0 && sec <= 3600 { config.ReadyTimeoutSec = sec; saveConfig() }
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
            case "snaptoagy":
                config.SnapToAgy = (val.lowercased() == "true" || val == "1"); saveConfig()
            case "hideonfullscreen":
                config.HideOnFullscreen = (val.lowercased() == "true" || val == "1"); saveConfig()
            case "accessory":
                let reqAcc = val.isEmpty ? "auto" : val.lowercased()
                if isAccessorySupportedForCurrentSkin() && isAccessoryUnlocked(reqAcc) { config.Accessory = reqAcc; saveConfig() }
            case "checkupdatesonstartup":
                config.CheckUpdatesOnStartup = (val.lowercased() == "true" || val == "1"); saveConfig()
            case "dockleft":
                dockSide = "left"; dockPeekOut = false
                dockIgnoreHoverUntil = Date().addingTimeInterval(0.7)
                window.setFrameOrigin(NSPoint(x: dockedTargetX(side: "left", peekOut: false), y: window.frame.origin.y))
            case "dockright":
                dockSide = "right"; dockPeekOut = false
                dockIgnoreHoverUntil = Date().addingTimeInterval(0.7)
                window.setFrameOrigin(NSPoint(x: dockedTargetX(side: "right", peekOut: false), y: window.frame.origin.y))
            case "quickdispatch":
                DispatchQueue.main.async { self.showQuickDispatchWindow() }
            case "updateconfig":
                if let sk = body["skin"] as? String, packs[sk] != nil { config.Skin = sk }
                if let sc = body["scale"] as? Double { applyScale(sc) }
                if let bm = body["bubbleMode"] as? String { config.BubbleMode = bm }
                if let lg = body["language"] as? String { config.Language = lg.lowercased() }
                if let aw = body["autoWalk"] as? Bool { config.AutoWalk = aw }
                if let ef = body["eyeFollow"] as? Bool { config.EyeFollow = ef }
                if let tm = body["topmost"] as? Bool { config.Topmost = tm; window.level = tm ? .floating : .normal }
                if let ab = body["autoStartOnBoot"] as? Bool { config.AutoStartOnBoot = ab }
                if let cu = body["checkUpdatesOnStartup"] as? Bool { config.CheckUpdatesOnStartup = cu }
                if let sa = body["soundAlert"] as? Bool { config.SoundAlert = sa }
                saveConfig()
            default: break
            }
            canvasView.needsDisplay = true
            syncWindowSizeForBubbles()
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
            "readyTimeoutSec": config.ReadyTimeoutSec,
            "snapToAgy": config.SnapToAgy,
            "hideOnFullscreen": config.HideOnFullscreen,
            "accessory": config.Accessory,
            "effectiveAccessory": effectiveAccessory(),
            "accessorySupported": isAccessorySupportedForCurrentSkin(),
            "autoWalk": config.AutoWalk,
            "eyeFollow": config.EyeFollow,
            "topmost": config.Topmost,
            "autoStartOnBoot": config.AutoStartOnBoot,
            "checkUpdatesOnStartup": config.CheckUpdatesOnStartup,
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

    // MARK: Startup Version Check & Self-Update

    func currentPluginVersion() -> String {
        let pUrl = pluginRoot.appendingPathComponent("plugin.json")
        if let data = try? Data(contentsOf: pUrl),
           let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
           let ver = obj["version"] as? String {
            return ver
        }
        return "0.0.0"
    }

    func compareSemver(_ a: String, _ b: String) -> Int {
        let pa = a.trimmingCharacters(in: CharacterSet(charactersIn: "vV ")).split(separator: ".").map { Int($0) ?? 0 }
        let pb = b.trimmingCharacters(in: CharacterSet(charactersIn: "vV ")).split(separator: ".").map { Int($0) ?? 0 }
        let n = max(pa.count, pb.count)
        for i in 0..<n {
            let va = i < pa.count ? pa[i] : 0
            let vb = i < pb.count ? pb[i] : 0
            if va != vb { return va < vb ? -1 : 1 }
        }
        return 0
    }

    func checkGitHubReleaseIfNeeded() {
        guard config.CheckUpdatesOnStartup else { return }
        guard let url = URL(string: "https://api.github.com/repos/Wlaeg/antigravity-pet/releases/latest") else { return }
        var req = URLRequest(url: url, timeoutInterval: 6.0)
        req.setValue("AntigravityPet-macOS", forHTTPHeaderField: "User-Agent")
        URLSession.shared.dataTask(with: req) { [weak self] data, _, _ in
            guard let self = self, let data = data,
                  let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let tag = obj["tag_name"] as? String else { return }
            let latest = tag.trimmingCharacters(in: CharacterSet(charactersIn: "vV "))
            let cur = self.currentPluginVersion()
            guard self.compareSemver(cur, latest) < 0 else { return }
            var zipUrl = (obj["zipball_url"] as? String) ?? ""
            if let assets = obj["assets"] as? [[String: Any]] {
                for ast in assets {
                    if let name = ast["name"] as? String, name.hasSuffix(".zip"),
                       let dl = ast["browser_download_url"] as? String {
                        zipUrl = dl
                        break
                    }
                }
            }
            guard !zipUrl.isEmpty else { return }
            DispatchQueue.main.async {
                self.availableUpdateVersion = latest
                self.availableUpdateZipUrl = zipUrl
                self.updatePromptShownAt = Date()
                self.canvasView.needsDisplay = true
                self.syncWindowSizeForBubbles()
            }
        }.resume()
    }

    func performSelfUpdate() {
        guard !isUpdatingSelf, let dlUrl = URL(string: availableUpdateZipUrl) else { return }
        isUpdatingSelf = true
        let isEn = I18n.effectiveLanguage(config: config) == "en"
        selfUpdateStatusMsg = isEn ? "Downloading v\(availableUpdateVersion)..." : "正在下载 v\(availableUpdateVersion)..."
        canvasView.needsDisplay = true
        syncWindowSizeForBubbles()

        var req = URLRequest(url: dlUrl, timeoutInterval: 30.0)
        req.setValue("AntigravityPet-macOS", forHTTPHeaderField: "User-Agent")
        URLSession.shared.downloadTask(with: req) { [weak self] tmpFile, _, err in
            guard let self = self else { return }
            if let err = err ?? (tmpFile == nil ? NSError(domain: "update", code: -1) : nil), tmpFile == nil {
                DispatchQueue.main.async {
                    self.isUpdatingSelf = false
                    self.selfUpdateStatusMsg = ""
                    self.canvasView.needsDisplay = true
                    _ = err
                }
                return
            }
            guard let tmpFile = tmpFile else { return }
            let stageDir = FileManager.default.temporaryDirectory.appendingPathComponent("agy-pet-update-\(UUID().uuidString)")
            try? FileManager.default.createDirectory(at: stageDir, withIntermediateDirectories: true)
            let zipDst = stageDir.appendingPathComponent("update.zip")
            try? FileManager.default.moveItem(at: tmpFile, to: zipDst)

            let script = """
            #!/bin/bash
            sleep 0.6
            unzip -o -q "\(zipDst.path)" -d "\(stageDir.path)/out"
            SRC="\(stageDir.path)/out"
            if [ ! -f "$SRC/plugin.json" ]; then
              SUB=$(find "$SRC" -maxdepth 2 -name "plugin.json" -print -quit)
              if [ -n "$SUB" ]; then SRC=$(dirname "$SUB"); fi
            fi
            if [ -f "$SRC/plugin.json" ]; then
              cp -R "$SRC"/* "\(self.pluginRoot.path)/"
              if [ -f "\(self.pluginRoot.path)/build.sh" ]; then
                bash "\(self.pluginRoot.path)/build.sh" --launch >/dev/null 2>&1 &
              fi
            fi
            rm -rf "\(stageDir.path)"
            """
            let shPath = stageDir.appendingPathComponent("apply.sh")
            try? script.write(to: shPath, atomically: true, encoding: .utf8)

            let proc = Process()
            proc.executableURL = URL(fileURLWithPath: "/bin/bash")
            proc.arguments = [shPath.path]
            try? proc.run()

            DispatchQueue.main.async {
                self.saveConfig()
                NSApp.terminate(nil)
            }
        }.resume()
    }
}

// MARK: - Entry Point

let app = NSApplication.shared
let delegate = PetController()
app.delegate = delegate
app.run()
