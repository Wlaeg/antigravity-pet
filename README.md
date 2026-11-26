<div align="center">

<img src="assets/preview.svg" alt="Antigravity Pet Preview Banner" width="100%" />

# 🐾 Antigravity Pet

**A native pixel-art desktop companion that turns your Google Antigravity AI workflow into a living desk buddy — zero extra runtime dependencies.**

[![Release](https://img.shields.io/badge/Release-v4.5.0-0EA5E9?style=flat-square)](https://github.com/Wlaeg/antigravity-pet/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20%7C%20macOS%2012%2B-0078D6?style=flat-square)](https://github.com/Wlaeg/antigravity-pet/releases/latest)
[![Characters](https://img.shields.io/badge/Pixel%20Pets-8%20Built--in%20%2B%20Community-F59E0B?style=flat-square)](#characters)
[![i18n](https://img.shields.io/badge/i18n-11%20Locales-10B981?style=flat-square)](#english)
[![License](https://img.shields.io/badge/License-MIT-A855F7?style=flat-square)](LICENSE)

<br/>

**[⬇️ Download Latest Release (v4.5.0)](https://github.com/Wlaeg/antigravity-pet/releases/latest)** · **[简体中文](#简体中文)** · **[English](#english)**

</div>

---

<a id="english"></a>

## English

Stop staring at spinning terminals. **Antigravity Pet** sits cleanly on your **Windows** or **macOS** desktop, shows what your AI agent is actually doing in real time, tracks background tasks without stealing focus, and reacts to your typing — all in one lightweight native overlay.

### 🔥 Why Install It?

- **Live Tool Status & Calm Background Mode (`v4.5.0`)**
  - **Real-time action bubbles**: See single-line live steps like `Reading server.mjs`, `Editing App.tsx`, or `Running tests` right above your pet instead of a generic spinner.
  - **Independent `BackgroundRunning` (`🌙`)**: When your foreground chat is idle while background tasks run, the pet switches to a calm lavender-blue resting pose with a `🌙` badge. Click stacked task bubbles to expand the list and jump straight to any session — and get a subtle non-intrusive head alert when done without stealing focus.
- **1 Static Image Comes Alive + Community Library (`v4.2–v4.3`)**
  - Drop **just 1 static PNG** into the built-in **Pack Studio** (or use the `hatch-pet` skill) and the procedural animation engine brings it to life with breathing, parabolic hops, and squash-and-stretch bounces.
  - Switch between **8 built-in mascots** or install new ones in 1 click from the **Community Pet Library** (`cyber-panda`, `pixel-octopus`, `coffee-slime`).
- **Playful Typing Sync & Tiered Boredom (`v4.3`)**
  - Bounces with your typing rhythm (privacy-first frequency only — **zero keylogging**); type faster than 5 keys/s and watch dizzy stars orbit its head.
  - Leave it alone and it wanders with `?` / `♪` cues before curling up for a `Zzz` nap — one keystroke wakes it in 0.3s.
- **Polished Everyday Controls (`v4.1–v4.4`)**
  - Right-click **Settings** to open a standalone `460×800` window with **5 clean tabs** (**Characters / Behavior / Bubbles / System / Updates**), `0.33x–3.00x` smooth scale slider, edge half-body peeking, fullscreen auto-hide, and one-click in-app self-updates.
- **11-Locale Full-Stack Localization (`v4.4–v4.5`)**
  - Automatically follows your Antigravity locale (or switch manually anytime) across the **Settings UI, pet names, live tool bubbles, background counters, approval buttons, and right-click menu**: `English`, `简体中文`, `繁體中文 (TW)`, `Français`, `Deutsch`, `日本語`, `한국어`, `हिन्दी`, `ไทย`, `Español (ES)`, and `Español (MX)`.

---

### 🚀 30-Second Quick Start

#### Windows 10 / 11
1. Download the [latest release `.zip`](https://github.com/Wlaeg/antigravity-pet/releases/latest) and extract it to:
   ```text
   %USERPROFILE%\.gemini\config\plugins\antigravity-pet\
   ```
2. Run the one-click launcher (uses built-in `.NET Framework 4.x`, **no extra SDK needed**):
   ```powershell
   cd "$env:USERPROFILE\.gemini\config\plugins\antigravity-pet"
   .\build.ps1 -Launch
   ```
3. Right-click the pet for **Settings**, or press **`Win + Alt + P`** anytime to show/hide.

#### macOS 12+ (Apple Silicon & Intel)
1. Download the [latest release `.zip`](https://github.com/Wlaeg/antigravity-pet/releases/latest) and extract it to:
   ```text
   ~/.gemini/config/plugins/antigravity-pet/
   ```
2. Build & launch with macOS built-in `swiftc` (**no Xcode project needed**):
   ```bash
   cd ~/.gemini/config/plugins/antigravity-pet
   chmod +x build.sh && ./build.sh --launch
   ```
3. Press **`⌥ + ⌘ + P`** (`Option + Command + P`) to toggle visibility, or open **Desktop Pet Settings** from Antigravity's `...` menu.

---

<a id="characters"></a>

### 🎭 Characters & Growth Accessories

Includes **8 built-in pixel pets** (`cyber-fox`, `astro-bot`, `shadow-cat`, `byte-ghost`, `shiba-dog`, `pixel-dragon`, `android-bugdroid`, `flutter-dash`), **3 one-click community packs** (`cyber-panda`, `pixel-octopus`, `coffee-slime`), and unlockable frame-synced accessories (**Glasses**, **Dev Cap**, **Coffee Cup**, **Gold Crown**).

---

### 📚 Docs & Developer Guides

Looking to craft your own pet pack or integrate with the local bridge? Detailed specs live in separate docs:

- 🎨 **[Custom Pet Pack Specification (v3)](CUSTOM_PET_SPEC.md)** — Single-image fallback, `1536×2288` (`8×11`) atlas layout, `themeColor`, custom `.wav` sounds, and `.zip` packaging.
- 🛠️ **[Architecture, Hooks & Local HTTP API](docs/DEVELOPMENT.md)** — System diagram, directory structure, `BackgroundRunning` priority state machine, and `127.0.0.1:47821` endpoints.
- 📋 **[Release Changelog](CHANGELOG.md)** — Full version history from `v3.0.0` to `v4.5.0`.

---

<a id="简体中文"></a>

## 简体中文

别再盯着枯燥的终端转圈了。**Antigravity Pet** 是一款专为 **Google Antigravity** 打造的原生跨平台（**Windows & macOS**）像素桌面宠物插件——**零第三方运行时依赖**，既能实时播报 AI 正在干嘛、优雅盯住后台多任务，还会在你敲代码时陪你互动。

### 🔥 为什么值得装？

- **真能干活：实时动作气泡 & 后台静息态 (`v4.5.0`)**
  - **单行实时工具播报**：气泡不再只是死板的“运行中”，而是实时显示 `正在读取 server.mjs`、`正在编辑 App.tsx`、`正在运行测试`（无省略号、自适应撑宽）。
  - **独立 `BackgroundRunning` (`🌙`)**：前台空闲、后台有子任务在跑时，桌宠切为柔和蓝紫静息姿态并亮起 `🌙` 角标；多任务堆叠时**点击气泡展开列表直达对应对话**，跑完只飘出轻提醒，**绝不抢走你正在敲代码的焦点**。
- **1 张静态图直接变活 + 社区宠物库 (`v4.2–v4.3`)**
  - 内置 **制作宠物包（Pack Studio）**：随手拖入 **1 张静态图**，程序化动画引擎自动驱动平滑呼吸、抛物线小跳与 Q 弹挤压回弹；也可以直接喊内置 `hatch-pet` 技能帮你孵化新角色。
  - 自带 **8 只内置像素宠** + **社区宠物库**（赛博小熊猫 `cyber-panda`、像素章鱼 `pixel-octopus`、咖啡史莱姆 `coffee-slime`）一键安装。
- **好玩有灵性：敲键蹦跶、手速晕与无聊发呆 (`v4.3`)**
  - 随打字频率同步弹跳（仅统计 1.5 秒滑窗频率，**绝不记录任何按键内容**），手速狂飙超过 5 键/秒头顶会冒 **3 颗旋转星星眩晕**！
  - 挂机不管它会自己溜达冒 `?` / `♪`，久了会压扁打盹飘 `Zzz`；敲一下键盘 0.3 秒瞬间惊醒。
- **打磨到位的日常体验 (`v4.1–v4.4`)**
  - 右键点击 **「⚙️ 设置」** 秒开纯净 `460×800` 独立设置窗：**角色 / 互动 / 气泡 / 系统 / 更新** 5 大分页、**`0.33x–3.00x` 无级缩放**、左右半身贴边藏匿、全屏自动隐身，以及设置页内一键版本自更新。
- **11 国/地区全链路多语言支持 (`v4.4–v4.5`)**
  - 支持自动跟随 Antigravity 客户端语言或随时手动切换，完整覆盖 **设置面板、角色名称、实时工具气泡、后台计数、审批按钮与桌宠右键菜单**：`English`、`简体中文`、`繁體中文（台湾）`、`Français`、`Deutsch`、`日本語`、`한국어`、`हिन्दी`、`ไทย`、`Español (ES)`、`Español (MX)`。

---

### 🚀 30 秒极速安装

#### Windows 10 / 11
1. 前往 **[Releases 页面下载最新 `.zip`](https://github.com/Wlaeg/antigravity-pet/releases/latest)**，解压到 Antigravity 插件目录：
   ```text
   %USERPROFILE%\.gemini\config\plugins\antigravity-pet\
   ```
2. 运行一键启动脚本（调用系统自带 `.NET Framework 4.x`，**无需额外安装 SDK**）：
   ```powershell
   cd "$env:USERPROFILE\.gemini\config\plugins\antigravity-pet"
   .\build.ps1 -Launch
   ```
3. 右键桌宠即可打开独立 **设置** 窗口，随时按 **`Win + Alt + P`** 显隐。

#### macOS 12+ (Apple Silicon & Intel)
1. 下载 **[最新 Release `.zip`](https://github.com/Wlaeg/antigravity-pet/releases/latest)** 并解压到：
   ```text
   ~/.gemini/config/plugins/antigravity-pet/
   ```
2. 执行一键构建脚本（调用系统自带 `swiftc`，**零额外依赖**）：
   ```bash
   cd ~/.gemini/config/plugins/antigravity-pet
   chmod +x build.sh && ./build.sh --launch
   ```
3. 按 **`⌥ + ⌘ + P`**（`Option + Command + P`）显隐桌宠，或在 Antigravity 右上角 `...` 菜单打开设置。

---

### 🎭 角色与成长配饰

内置 **8 款经典角色**（小蓝狐 `cyber-fox`、宇航员 `astro-bot`、小黑猫 `shadow-cat`、电子幽灵 `byte-ghost`、柴犬 `shiba-dog`、小青龙 `pixel-dragon`、安卓小绿人 `android-bugdroid`、Flutter 小蓝鸟 `flutter-dash`）与 **3 款社区宠物**，并支持随完成任务数解锁 **墨镜、鸭舌帽、咖啡杯、黄金皇冠** 帧同步配饰。

---

### 📚 进阶与开发者文档

想自己画桌宠、做宠物包，或调用本地控制接口？详细开发文档已独立拆出：

- 🎨 **[自定义宠物包规范 (v3)](CUSTOM_PET_SPEC.md)** — 单图保底、`1536×2288` (`8×11`) 图集规范、`themeColor` 与自定义 `.wav` 音效打包说明。
- 🛠️ **[系统架构、Hooks 与本地 HTTP 接口](docs/DEVELOPMENT.md)** — Mermaid 架构图、目录结构、`BackgroundRunning` 优先级状态机与 `127.0.0.1:47821` API 文档。
- 📋 **[版本更新日志 (Changelog)](CHANGELOG.md)** — `v3.0.0` 至 `v4.5.0` 完整变更记录。

---

### ❓ 常见问题 (FAQ)

- **Q：为什么任务栏或 Dock 里看不到桌宠？**
  - A：特意采用了不占任务栏/Dock 的纯净悬浮窗模式（Windows `WS_EX_TOOLWINDOW` / macOS `LSUIElement`），按 `Win + Alt + P`（macOS 为 `⌥ + ⌘ + P`）或右键即可随时控制。
- **Q：会开机自启占后台吗？**
  - A：默认 **「随 Antigravity 启动」**，打开 Antigravity 时拉起、关闭时自动退出，不写开机自启项。

---

<div align="center">
Released under the <a href="LICENSE">MIT License</a>.
</div>
