# Changelog

All notable changes to **Antigravity Pet** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
