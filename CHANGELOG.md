# Changelog

All notable changes to **Antigravity Pet** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.1.1] - 2026-11-25

### Fixed
- **Windows DPI-Aware Pet Sizing (`app/AntigravityPet.cs`)**:
  - Enabled Per-Monitor V2 DPI awareness on startup (`EnablePerMonitorDpiAwareness`) and removed the inverse `1 / dpi` layout and window dimension scaling in `ApplyScale`, `SetWindowSizeKeepingAnchor`, `OnAnimTick`, and `IsCursorInDockedZone`, so the pet scales naturally with Windows DPI settings and maintains a consistent visual size across 100%, 125%, 150%, and 200% DPI displays.

### Changed
- **3x Logarithmic Scale Slider in Settings UI (`ui/settings.html`, `scripts/pet-mcp-server.mjs`)**:
  - Replaced discrete size preset buttons in the MCP App settings view (`ui/settings.html`) with the continuous `0.33x`–`3.00x` logarithmic slider (matching the sidecar panel, with live preview and one-click `1.00x` reset), and updated the MCP `scale` schema description accordingly.

## [4.1.0] - 2026-11-25

### Added
- **GitHub Self-Update Checker & One-Click In-Place Updater (`app/AntigravityPet.cs`)**:
  - Checks `releases/latest` on startup (controlled by the new **Check for Updates on Startup** toggle in Settings).
  - Displays a purple update bubble (`New release vX.Y.Z · Click to update`) that follows `ReadyTimeoutSec` auto-hide rules; clicking downloads the `.zip` release, replaces plugin files in place, and relaunches `AntigravityPet.exe` automatically.
- **Windows Tablet Touch Dragging (`app/AntigravityPet.cs`)**:
  - Added native WPF `TouchDown` / `TouchMove` / `TouchUp` / `LostTouchCapture` support with press-and-hold/flick suppression and logical DPI coordinate conversion, plus 350ms double-tap quick dispatch.
- **Settings Entry in Context Menu & Panel Edge Docking (`app/AntigravityPet.cs`, `sidecars/panel/`)**:
  - Added **Settings (Antigravity)** in the pet context menu to focus Antigravity and guide users to the sidecar panel, and added **Dock Left** / **Dock Right** buttons in `sidecars/panel/index.html`.

### Changed
- **Streamlined Context Menu with Inline Pet Switcher (`app/AntigravityPet.cs`)**:
  - Replaced hover-based submenu popups with an inline click-to-expand `Switch Pet` list (`StaysOpenOnClick`), eliminating accidental submenu dismissal on diagonal cursor movement, and trimmed the menu to essential high-frequency actions.
- **Click Behavior Split (`app/AntigravityPet.cs`)**:
  - Clicking the pet body now plays playful Jump/Wave animations only, while clicking the status bubble opens the corresponding Antigravity conversation.
- **Bubble Spacing & Unified Running Color (`app/AntigravityPet.cs`)**:
  - Tightened bubble-to-pet spacing via `1* / Auto` grid rows, unified `Running` state badges and docked blocks to `#0EA5E9` blue, and prioritized pending approvals (`!`) on the corner badge.
- **Settings UI Polish (`sidecars/panel/index.html`, `ui/settings.html`)**:
  - Refined spacing to a strict `4px` grid, nested `8px/12px` radii, normalized type scale, `1.5px` monochrome icons, and `:focus-visible` keyboard rings per `unslop` guidelines.

### Fixed
- **Display / Resolution Change Bounds Clamping (`app/AntigravityPet.cs`)**:
  - Listens to `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, and `SystemEvents.DisplaySettingsChanged`, clamping coordinates to the nearest active monitor work area via `GetCurrentMonitorWorkArea()` so the pet never gets stranded off-screen after resolution or monitor changes.
- **Docked Edge Auto-Peek Lock (`app/AntigravityPet.cs`)**:
  - Added `_requireHoverExitBeforePeek` and touch-release cursor anchor suppression so docked pets stay tucked after mouse or touch release until the cursor leaves and re-enters the edge zone.
- **Panel Dispatch Bridge Path & SDK Alignment (`app/AntigravityPet.cs`, `sidecars/panel/server.mjs`)**:
  - Unified `panel-bridge.json` lookup to `~/.antigravity-pet/panel-bridge.json` and wired `startConversation` support in `server.mjs`.

## [4.0.1] - 2026-11-25

### Fixed
- **Non-Blocking HTTP `/approval` Handling (`app/AntigravityPet.cs`)** — Reported by `dllhell`:
  - Offloaded `HandleRequest(ctx)` in `StartHttpServer()` to `ThreadPool.QueueUserWorkItem` with request-scoped `JavaScriptSerializer` instances so concurrent `/health` and `/state` requests from subagents or other chats no longer hit timeouts while an approval bubble is open.
- **Local `/approval` Token Lock-Down (`app/AntigravityPet.cs`, `scripts/hook-bridge.mjs`)**:
  - Generated a per-launch 128-bit random token written to `~/.antigravity-pet/http-bridge.json` on startup and required it via `X-Pet-Token` / `token` on `POST /approval` (`403 Forbidden` when missing or mismatched).
- **Antigravity IDE Detection (`app/AntigravityPet.cs`)**:
  - Updated process detection (`OnBehaviorTick`, `TryGetAntigravityWindowRect`, `OpenAntigravityConversation`) and the executable launcher to match both `"Antigravity"` and `"Antigravity IDE"` (`Programs\Antigravity IDE\Antigravity IDE.exe`).
- **MCP Server Namespace (`mcp_config.json`)**:
  - Renamed the MCP server entry from `"pet"` to `"antigravity-pet"` to prevent global MCP server namespace collisions.

## [4.0.0] - 2026-11-24

### Added
- **Reverse Control — Double-Click Quick Dispatch & In-Bubble Approval (`PreToolUse`)**:
  - **Double-click** the pet on Windows or macOS to open a lightweight prompt dialog and dispatch tasks directly to Antigravity via `SidecarApp.sendMessage()` (with a `240ms` single-click anti-conflict guard).
  - High-risk `run_command` tool calls intercepted by `hooks.json` (`PreToolUse`) pause synchronously on the pet bubble with **✓ Allow / ✕ Deny** buttons (`45s` timeout safely falls back to `"ask"`), while safe commands pass through with zero latency.
- **Screen-Edge Half-Body Peeking & Compact Status Block**:
  - Drag the pet near the left or right edge of any monitor (or use the right-click **Dock to Screen Edge** menu) to tuck half its body off-screen with a subtle `14°` peek tilt and a `700ms` release cooldown.
  - While docked and unhovered, the chat bubble collapses into a crisp `34×14` high-contrast status color block above the visible half-body; hovering over the dual-hysteresis screen zone (`260ms` debounce) smoothly peeks the pet out with the full bubble.
- **Fullscreen Auto-Hide & Urgent Fullscreen Edge Peek**:
  - Dual fullscreen detection (`SHQueryUserNotificationState` + foreground monitor coverage check on Windows; `CGWindowListCopyWindowInfo` on macOS) hides the pet during fullscreen games/videos and automatically peeks it out on the screen edge without stealing focus whenever `Ready`, `Needs Input`, `Blocked`, or an approval prompt occurs.
  - Added optional **Snap to Antigravity Window** (`snapToAgy`) docking to the top-right titlebar edge.
- **Growth Pixel Accessories (Frame-Synced)**:
  - Added pixel **Glasses**, **Dev Cap**, **Coffee Cup** (animated steam), and **Gold Crown** unlocked by completed task count or selectable manually from the Settings Panel / context menu.
  - Accessories are composited directly into each sprite animation frame with a `1px` dark pixel outline, syncing 1:1 with breathing, walking, jumping, `flipX` mirroring, and drag physics.
- **Custom Pet Pack v3 Spec & `.zip` Importer**:
  - Added `spriteVersionNumber: 3` support for `themeColor`, custom `.wav` `sounds` per state, and per-action frame folder fallback (`idle/`, `running/`, etc.), plus one-click `.zip` pet pack import in the Settings Panel (`POST /api/import-pet`).

### Changed
- **8 Character-Specific Working Props & Pixel Bubbles**: Re-authored `Row 7` working animations with unified `1px` outlines and 6-frame motion for all 8 built-in pets; redesigned status bubbles with `2px` pixel corners, hard drop shadows, monospace typography, and `themeColor` accents.
- **Professional IDE Settings Panel**: Redesigned `sidecars/panel/index.html` and `ui/settings.html` with neutral slate surfaces and crisp `1.5px` monochrome SVG icons.

## [3.3.0] - 2026-11-24

### Added
- **Bubble Style Switch: Simple Mode & Detailed Stacked Mode (`bubbleStyle: "simple" | "detailed"`)**:
  - **Simple Mode** *(Default)*: Classic single bubble showing the highest-priority status and concurrent task count.
  - **Detailed Mode**: Displays bubbles in **`Task Name - State`** format (e.g. `Refactor login - Editing code...`). When multiple conversations run concurrently, bubbles visually stack (`+N` badge with layered backing cards) and **automatically expand upward on mouse hover** into an interactive list where each row opens its exact conversation on click.
- **Conversation Pinning (`pinnedConversationId`)**: Pin (`📌`) a specific conversation from the expanded bubble list or Settings Panel so the pet stays locked onto that task until unpinned.
- Full cross-platform sync across Windows (`AntigravityPet.cs`), macOS (`AntigravityPet.swift`), Sidecar Panel (`sidecars/panel/`), and MCP App (`ui/settings.html`).

## [3.2.2] - 2026-11-24

### Fixed
- **Windows Always on Top (`Topmost`) & Multi-Monitor Support (`app/AntigravityPet.cs`)** — Thanks to [@bikash-izaki](https://github.com/bikash-izaki) in [#2](https://github.com/Wlaeg/antigravity-pet/pull/2) (fixes [#1](https://github.com/Wlaeg/antigravity-pet/issues/1)):
  - Fixed startup `config.json` `Topmost` reset by applying `Topmost = _config.Topmost` before `SaveConfig()` and removing the premature overwrite inside `SaveConfig()`.
  - Added `ApplyTopmost()` using Win32 `SetWindowPos(HWND_TOPMOST / HWND_NOTOPMOST)` with `SWP_NOACTIVATE` and `SWP_FRAMECHANGED`, and re-asserted z-order in `OnBehaviorTick` so the pet reliably stays above active Chromium/Electron windows like Antigravity.
  - Exposed live `win32Topmost` (`WS_EX_TOPMOST`) status in `GET /health`.
  - Switched `RestoreWindowPosition()` and `ClampToWorkArea()` to `SystemParameters.VirtualScreen*` so the pet can remain on secondary monitors in multi-monitor setups.

## [3.2.1] - 2026-11-23

### Fixed
- **Zero-Offset High-DPI Dragging (`app/AntigravityPet.cs`)**: Fixed cursor-to-pet position drift during window dragging on Windows high-DPI displays (125%/150%/200%) by converting Win32 `GetCursorPos` physical coordinates via `CompositionTarget.TransformFromDevice` into WPF logical coordinates.

### Added
- **Interactive Drag Reaction Physics (`app/AntigravityPet.cs`, `app/AntigravityPet.swift`)**: Added lively "picked-up" drag reaction animation on both Windows and macOS — velocity-driven pendulum tilt (`±22°`), elastic stretch, floating shadow dim, accelerated airborne struggle frames, and smooth spring-back bounce on release.

## [3.2.0] - 2026-11-23

### Added
- **2 Classic Google Developer Mascots (8 Built-in Characters Total)**:
  - `android-bugdroid` (**Android Bugdroid / 安卓小绿人**): Classic `#3DDC84` green Android robot with dual antennas, segmented body, and white eyes.
  - `flutter-dash` (**Flutter Dash / Flutter 小蓝鸟**): Classic `#54C5F8` / `#0175C2` Flutter & Dart blue bird mascot with crest, yellow beak, flapping wings, and orange feet.
- Added alias support (`android`, `bugdroid`, `dash`, `flutter`), localized English/Chinese descriptions, and UI icons across `AntigravityPet.cs`, `AntigravityPet.swift`, `server.mjs`, `pet-mcp-server.mjs`, `index.html`, and `settings.html`.

## [3.1.0] - 2026-11-22

### Added
- **macOS Native Support (`app/AntigravityPet.swift`, `build.sh`)**:
  - Native Swift AppKit transparent floating overlay with `LSUIElement` + `.accessory` policy (zero Dock or Cmd+Tab clutter).
  - Global show/hide hotkey **`⌥ + ⌘ + P`** (`Option + Command + P`) via Carbon HotKey API.
  - Nearest-neighbor (`interpolationQuality = .none`) rendering for crisp `36×39` pixel animations, 16-direction eye tracking, idle roaming, and click-to-jump (`antigravity://conversation/<id>`).
  - Built-in Darwin POSIX Socket HTTP server on `127.0.0.1:47821` matching all Windows endpoints (`/health`, `/state`, `/skin`, `/reload`, `/control`).
  - Automatic exit when Antigravity closes (`Launch with Antigravity` mode).
- **Cross-Platform Bridges (`scripts/hook-bridge.mjs`, `sidecars/panel/server.mjs`, `scripts/pet-mcp-server.mjs`)**:
  - Automatic OS detection for `config.json` path (`~/Library/Application Support/AntigravityPet/` on macOS, `%LOCALAPPDATA%\AntigravityPet\` on Windows) and Antigravity locale files.
  - Auto-builds & launches `AntigravityPet.app` via `build.sh --launch` on macOS if not compiled yet.
- **CHANGELOG.md** for standardized GitHub Releases.

### Changed
- Updated `plugin.json` version to `3.1.0`.
- Updated Settings Panel UI (`sidecars/panel/index.html`, `ui/settings.html`) and `README.md` for Windows & macOS dual-platform support.

## [3.0.0] - 2026-11-22

### Added
- **EN / 简体中文 Bilingual Support**: Auto-follows Antigravity client language (`Auto`, `English`, `简体中文`) across bubbles and Settings Panel.
- **Launch & Exit with Antigravity**: Replaced Windows startup registry entry with automatic lifecycle sync alongside Antigravity.
- **Built-in Aux Pane Settings Panel (`...` menu)** & MCP App UI (`ui/settings.html`) with zero taskbar/tray clutter (`WS_EX_TOOLWINDOW`, `Win + Alt + P`).
- **6 Built-in `36×39` Pixel Characters**: `cyber-fox`, `astro-bot`, `shadow-cat`, `byte-ghost`, `shiba-dog`, `pixel-dragon`, plus `/hatch-pet` skill.
- **Multi-session State Arbitration & Click-to-Jump**: `Needs Input > Blocked > Ready > Running > Idle` with instant window focus and conversation deep-linking.
