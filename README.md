<div align="center">

<img src="assets/preview.svg" alt="Antigravity Pet Preview Banner" width="100%" />

# 🐾 Antigravity Pet

**A native Windows & macOS pixel desktop companion plugin for Google Antigravity — zero third-party runtime dependencies.**

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20%7C%20macOS%2012%2B-0078D6?style=flat-square)](https://github.com/Wlaeg/antigravity-pet)
[![Engine](https://img.shields.io/badge/Engine-C%23%20WPF%20%7C%20Swift%20AppKit-38BDF8?style=flat-square)](app)
[![Characters](https://img.shields.io/badge/Pixel%20Pets-8%20Built--in-F59E0B?style=flat-square)](pets)
[![i18n](https://img.shields.io/badge/i18n-English%20%2F%20%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-10B981?style=flat-square)](#english)
[![License](https://img.shields.io/badge/License-MIT-A855F7?style=flat-square)](LICENSE)

[English](#english) · [简体中文](#简体中文)

</div>

---

<a id="english"></a>

## English

**Antigravity Pet** brings a lightweight, reactive pixel-art companion directly to your Windows & macOS desktop while you work with **Google Antigravity**. Powered by a native transparent overlay (**C# WPF** on Windows / **Swift AppKit** on macOS), Antigravity lifecycle hooks, and an integrated Aux Pane Settings Panel (`...` menu & MCP App), it works out of the box with zero external SDKs or runtimes.

### ✨ Highlights

- **Reverse Control: Double-Click Quick Dispatch & In-Bubble Approval**
  - **Double-click** the pet to open a quick prompt box and send tasks directly to Antigravity without switching windows (single-click still jumps straight to the active conversation via `antigravity://conversation/<id>`).
  - High-risk commands intercepted via `PreToolUse` show **✓ Allow / ✕ Deny** buttons right inside the pet's pixel bubble (`45s` timeout safely falls back to `"ask"`).
- **Screen-Edge Half-Body Peeking & Compact Status Block**
  - Drag the pet to the left or right edge of any display to dock it half-hidden with a `14°` peek tilt; the bubble simplifies into a `34×14` high-contrast status color block and smoothly expands back out on hover.
- **Fullscreen Auto-Hide & Urgent Edge Peek**
  - Automatically hides during fullscreen games or videos, and peeks onto the screen edge without stealing focus whenever a task finishes (`Ready`), needs input/approval, or encounters an error (`Blocked`).
  - Optional **Snap to Antigravity Window** keeps the pet docked to the top-right edge of your IDE.
- **Growth Pixel Accessories & Character-Specific Working Props**
  - Earn and equip frame-synced pixel **Glasses**, **Dev Cap**, **Coffee Cup**, and **Gold Crown** as you complete tasks; all 8 built-in pets feature custom animated working props and `themeColor` pixel bubbles.
- **Zero Taskbar/Dock Clutter & Built-in Settings Panel (`...` Menu)**
  - Uses `WS_EX_TOOLWINDOW` (Windows) and `LSUIElement` (macOS) with **`Win + Alt + P`** / **`⌥ + ⌘ + P`** global hotkey, **Launch with Antigravity**, EN/ZH auto-sync, **Detailed Stacked Bubbles** with `📌 Pin`, and one-click **`.zip` Custom Pet Pack v3 Import**.

---

### 🎭 Built-in Pixel Characters

| ID | Display Name | Style & Personality | Pack Location |
| :--- | :--- | :--- | :--- |
| `cyber-fox` | **CyberFox** *(Default)* | Neon cyan cyber fox with glowing accents | [`pets/cyber-fox`](pets/cyber-fox) |
| `astro-bot` | **AstroBot** | Mini space explorer with animated visor | [`pets/astro-bot`](pets/astro-bot) |
| `shadow-cat` | **ShadowCat** | Nimble dark cat with bright golden eyes | [`pets/shadow-cat`](pets/shadow-cat) |
| `byte-ghost` | **ByteGhost** | Playful floating pixel spirit | [`pets/byte-ghost`](pets/byte-ghost) |
| `shiba-dog` | **ShibaDog** | Warm golden Shiba Inu companion | [`pets/shiba-dog`](pets/shiba-dog) |
| `pixel-dragon` | **PixelDragon** | Emerald mini dragon with golden horns | [`pets/pixel-dragon`](pets/pixel-dragon) |
| `android-bugdroid` | **Android Bugdroid** | Classic Google Android green robot mascot | [`pets/android-bugdroid`](pets/android-bugdroid) |
| `flutter-dash` | **Flutter Dash** | Official Google Flutter & Dart blue bird mascot | [`pets/flutter-dash`](pets/flutter-dash) |

> Custom v3 pet packs (`themeColor`, custom `.wav` `sounds`, or per-action frame folders) can be imported directly via **Import .zip** in the Settings Panel, or placed inside `%USERPROFILE%\.antigravity-pet\pets\<pet-id>\`.

---

### 🔔 Multi-Session Priority & Status Machine

When multiple Antigravity conversations run concurrently, the pet arbitrates display state strictly by priority:

| Priority | State | Trigger (`hooks.json`) | Visual & Bubble Feedback |
| :---: | :--- | :--- | :--- |
| **1** | `Needs Input` | User confirmation / question required | Amber halo + `!` badge + prompt bubble + optional chime |
| **2** | `Blocked` | Tool error or execution failure | Red halo + `✕` badge + error bubble (click to inspect) |
| **3** | `Ready` | Agent task finishes (`Stop`) | Green celebration loop + `✓` badge + clickable completion bubble |
| **4** | `Running` | Thinking, file edits, commands (`PreInvocation` / `PostToolUse`) | Cyan halo + busy working animation + live step bubble |
| **5** | `Idle` | No active tasks | Gentle breathing + idle desktop roaming + 16-dir eye follow |

---

### 🧩 Architecture & Project Structure

```mermaid
flowchart LR
    AGY["Google Antigravity"] -->|hooks.json| Bridge["scripts/hook-bridge.mjs"]
    AGY -->|Aux Pane sidecar://| Panel["sidecars/panel/server.mjs"]
    AGY -->|MCP App ui://| MCP["scripts/pet-mcp-server.mjs"]
    Panel -->|Renders UI| PUI["sidecars/panel/index.html"]
    Bridge -->|HTTP 127.0.0.1:47821| Pet["AntigravityPet (WPF / AppKit)"]
    Panel -->|HTTP / config.json| Pet
    MCP -->|HTTP / config.json| Pet
    Pet -->|Click Bubble or Pet| DeepLink["OS Focus + antigravity://conversation/id"]
    DeepLink --> AGY
```

```text
antigravity-pet/
├── app/
│   ├── AntigravityPet.cs            # Windows native WPF overlay, HTTP server & hotkey
│   ├── AntigravityPet.exe           # Prebuilt zero-dependency Windows executable
│   └── AntigravityPet.swift         # macOS native AppKit overlay, HTTP server & hotkey
├── assets/
│   ├── logo.svg                     # Plugin icon
│   └── preview.svg                  # Repository showcase banner
├── pets/                            # 6 built-in 8x11 pixel-art sprite packs
├── scripts/
│   ├── generate-default-pets.ps1    # Procedural 36x39 pixel-art spritesheet generator
│   ├── hook-bridge.mjs              # Cross-platform Antigravity hooks -> HTTP bridge
│   └── pet-mcp-server.mjs           # Cross-platform MCP server + MCP App UI provider
├── sidecars/panel/
│   ├── sidecar.json                 # Aux Pane integration manifest (... menu)
│   ├── server.mjs                   # Cross-platform Sidecar backend & auto-launcher
│   └── index.html                   # Bilingual settings panel UI
├── skills/
│   ├── pet/SKILL.md                 # Skill guide for controlling the desktop pet
│   └── hatch-pet/SKILL.md           # Skill guide for generating custom pixel pets
├── ui/settings.html                 # MCP App standalone settings UI
├── build.ps1                        # Windows one-click builder & launcher
├── build.sh                         # macOS one-click Swift compiler & .app packager
├── hooks.json                       # Antigravity hook bindings
├── mcp_config.json                  # MCP server configuration
├── plugin.json                      # Antigravity plugin manifest
└── README.md
```

---

### 🚀 Installation & Quick Start

#### Windows 10 / 11

1. Clone or copy this repository into your Antigravity plugins directory:
   ```text
   %USERPROFILE%\.gemini\config\plugins\antigravity-pet\
   ```
2. Run the one-click build & launch script (uses built-in `.NET Framework 4.x` `csc.exe`, **no extra SDK required**):
   ```powershell
   cd "$env:USERPROFILE\.gemini\config\plugins\antigravity-pet"
   .\build.ps1 -Launch
   ```
3. Reload or open **Google Antigravity**, then click **`...`** at the top-right to open **Desktop Pet Settings** (or press **`Win + Alt + P`** anytime to toggle visibility).

#### macOS 12+ (Apple Silicon & Intel)

1. Clone or copy this repository into your Antigravity plugins directory:
   ```text
   ~/.gemini/config/plugins/antigravity-pet/
   ```
2. Run the one-click Swift build & launch script (uses macOS built-in `swiftc`, **no extra SDK required**):
   ```bash
   cd ~/.gemini/config/plugins/antigravity-pet
   chmod +x build.sh && ./build.sh --launch
   ```
   *(If Xcode Command Line Tools are not installed yet, run `xcode-select --install` first. Antigravity hooks will also auto-compile on first start.)*
3. Open **Google Antigravity**, click **`...`** at the top-right for **Desktop Pet Settings**, or press **`⌥ + ⌘ + P`** (`Option + Command + P`) anytime to show/hide.

---

### 🎨 Custom Pet Spritesheet Specification (`v2`, `8×11` Grid)

Each custom pet pack only requires two files: `pet.json` and `spritesheet.png` (32-bit RGBA transparent PNG, `1536×2288 px`, each cell `192×208 px`):

```json
{
  "id": "my-pet",
  "displayName": "MyPet",
  "description": "Custom pixel companion",
  "spriteVersionNumber": 2,
  "spritesheetPath": "spritesheet.png"
}
```

| Row | Animation State | Recommended Frames | Notes |
| :---: | :--- | :---: | :--- |
| `0` | `idle` | 6 | Gentle idle breathing & blinking |
| `1` | `running-right` | 8 | Walking right during desktop roaming |
| `2` | `running-left` | 8 | Walking left during desktop roaming |
| `3` | `waving` | 4 | Friendly wave (triggered on cursor greeting) |
| `4` | `jumping` | 5 | Joyful jump animation |
| `5` | `failed` | 8 | Error / blocked state (`Blocked`) |
| `6` | `waiting` | 6 | Waiting for confirmation (`Needs Input`) |
| `7` | `running` | 6 | Busy working animation (`Running`) |
| `8` | `review` | 6 | Task completion celebration (`Ready`) |
| `9–10` | `look-directions` | 16 (8 + 8) | Clockwise gaze directions (0 = Right, 4 = Down, 8 = Left, 12 = Up) |

---

### 🌐 Local HTTP Control API (`http://127.0.0.1:47821`)

| Endpoint | Method | Description | Example Payload |
| :--- | :---: | :--- | :--- |
| `/health` | `GET` | Returns running status, i18n language, full config, stats, recent chats, and pet list | — |
| `/state` | `POST` | Updates conversation state (`idle` / `running` / `needs_input` / `ready` / `blocked`) | `{"conversationId":"abc","state":"running","message":"Thinking..."}` |
| `/skin` | `POST` | Switches the active character pack | `{"skin":"cyber-fox"}` |
| `/reload` | `POST` | Hot-reloads pet packs from `pets/` and `%USERPROFILE%\.antigravity-pet\pets\` | `{}` |
| `/control` | `POST` | Executes window & config actions (`show`/`hide`/`toggle`/`open`/`quit`/`language`/`scale`/`bubbleMode`/`updateConfig`) | `{"action":"language","value":"en"}` |

---

<a id="简体中文"></a>

## 简体中文

**Antigravity Pet** 是专为 **Google Antigravity** 打造的原生跨平台（**Windows & macOS**）像素风桌面宠物插件。Windows 端基于原生 C# WPF 透明悬浮窗（`WS_EX_TOOLWINDOW`），macOS 端基于原生 Swift AppKit 悬浮窗（`LSUIElement`），结合 Antigravity 生命周期 Hooks 与客户端右上角 `...` 内置设置面板，**零第三方运行时依赖，开箱即用**。

### ✨ 核心亮点

- **反向控制：双击快捷派活 & 气泡快捷审批**
  - **双击桌宠本体** 即可呼出极简指令框直接向 Antigravity 派活（内置 `240ms` 防冲突保护，单击仍正常跳转关联对话）。
  - 通过 `hooks.json` (`PreToolUse`) 拦截高危命令时，桌宠头顶气泡直接弹出 **✓ 允许 / ✕ 拒绝** 按钮（45 秒超时自动回退系统确认）。
- **左右贴边半身观察 & 极简状态色块**
  - 将宠物拖到当前屏幕左/右边缘（或通过右键「📌 屏幕贴边停靠」），宠物自动藏起半身并微倾 `14°` 观察；收起时聊天框精简为 `34×14` 高对比度状态色块，鼠标悬停即平滑探出显示完整气泡。
- **全屏自动避让 & 关键提醒贴边弹出**
  - 全屏游戏或看视频时自动隐藏避让；一旦出现任务完成（`Ready`）、需要输入/审批或报错（`Blocked`），立即无焦点置顶贴边弹出提醒，处理后自动收回。支持可选 **吸附 Antigravity 窗口** 边缘。
- **成长像素配饰 & 8 款角色专属工作抱物**
  - 随完成任务数自动解锁（或手动佩戴）**像素墨镜、鸭舌帽、咖啡杯、黄金皇冠**，帧级合成进角色动画层并同步左右镜像与拖拽物理效果；8 款内置角色均配备专属 `1px` 描边工作抱物与类像素风主题色气泡。
- **零任务栏/Dock 干扰 & 专业内置设置面板**
  - Windows `WS_EX_TOOLWINDOW` / macOS `LSUIElement` 零图标驻留，支持 **`Win + Alt + P`** / **`⌥ + ⌘ + P`** 全局热键、随 Antigravity 启停、中英文自动跟随、**详细堆叠气泡 + `📌` 会话固定**，以及 **`.zip` 自定义宠物包一键导入**。

---

### 🎭 内置 8 款像素角色图鉴

| 皮肤 ID | 角色名称 | 外观特色 | 目录位置 |
| :--- | :--- | :--- | :--- |
| `cyber-fox` | **小蓝狐 CyberFox** *(默认)* | 赛博蓝荧光耳廓与灵动眼神 | [`pets/cyber-fox`](pets/cyber-fox) |
| `astro-bot` | **像素宇航员 AstroBot** | 发光头盔护目镜与太空服造型 | [`pets/astro-bot`](pets/astro-bot) |
| `shadow-cat` | **小黑猫 ShadowCat** | 金瞳神秘黑猫与精致铃铛 | [`pets/shadow-cat`](pets/shadow-cat) |
| `byte-ghost` | **电子幽灵 ByteGhost** | 半透明霓虹飘浮小幽灵 | [`pets/byte-ghost`](pets/byte-ghost) |
| `shiba-dog` | **像素柴犬 ShibaDog** | 暖黄色蓬松柴犬伙伴 | [`pets/shiba-dog`](pets/shiba-dog) |
| `pixel-dragon` | **像素小青龙 PixelDragon** | 翡翠绿身躯配金色小龙角 | [`pets/pixel-dragon`](pets/pixel-dragon) |
| `android-bugdroid` | **安卓小绿人 Bugdroid** | Google Android 官方经典绿色小机器人 | [`pets/android-bugdroid`](pets/android-bugdroid) |
| `flutter-dash` | **Flutter 小蓝鸟 Dash** | Google Flutter & Dart 官方吉祥物小蓝鸟 | [`pets/flutter-dash`](pets/flutter-dash) |

> 支持在设置面板点击 **「导入宠物包」** 一键上传 `.zip`（支持 v3 `themeColor`、自定义 `.wav` 音效与分动作序列帧目录），或直接放入 `%USERPROFILE%\.antigravity-pet\pets\<pet-id>\` 热重载启用。

---

### 🔔 多会话状态仲裁与视觉反馈

当多个 Antigravity 对话并行执行时，桌宠严格按以下优先级展示最高优先级的会话状态：

| 优先级 | 状态 | 触发场景 (`hooks.json`) | 桌宠表现与交互 |
| :---: | :--- | :--- | :--- |
| **1** | `Needs Input` | Agent 需要用户审批或回答问题 | 琥珀色光环 + `!` 徽章 + 提示气泡 + 可选轻柔提示音 |
| **2** | `Blocked` | 工具调用报错或执行阻断 | 红色光环 + `✕` 徽章 + 报错气泡，点击直达排查 |
| **3** | `Ready` | 任务完成 (`Stop`) | 绿色光环 + 庆祝动画 + `✓` 徽章，点击打开结果对话 |
| **4** | `Running` | 思考规划、读写文件、运行命令 | 冰蓝色光环 + 忙碌动画 + 实时进度气泡 |
| **5** | `Idle` | 所有会话空闲 | 呼吸眨眼 + 每隔十几秒桌面散步 + 16 方向眼神跟随 |

---

### 🚀 安装与一键编译指南

#### Windows 10 / 11

1. 克隆或复制本项目到 Antigravity 插件目录：
   ```text
   %USERPROFILE%\.gemini\config\plugins\antigravity-pet\
   ```
2. 执行一键构建与启动脚本（直接调用 Windows 自带 `.NET Framework 4.x` `csc.exe`，**无需安装额外 SDK**）：
   ```powershell
   cd "$env:USERPROFILE\.gemini\config\plugins\antigravity-pet"
   .\build.ps1 -Launch
   ```
3. 打开 **Google Antigravity**，点击右上角 **`...`** 插件菜单即可打开 **「桌面宠物设置」** 面板；随时可使用快捷键 **`Win + Alt + P`** 显隐桌宠。

#### macOS 12+ (Apple Silicon & Intel)

1. 克隆或复制本项目到 Antigravity 插件目录：
   ```text
   ~/.gemini/config/plugins/antigravity-pet/
   ```
2. 执行一键编译与启动脚本（调用系统自带 `swiftc` 编译为 `.app` 悬浮应用，**无需安装额外 SDK**）：
   ```bash
   cd ~/.gemini/config/plugins/antigravity-pet
   chmod +x build.sh && ./build.sh --launch
   ```
   *（若未安装命令行编译工具，系统提示时执行 `xcode-select --install` 即可；后续在 Antigravity 触发 Hooks 时也会自动检测并编译启动。）*
3. 打开 **Google Antigravity**，点击右上角 **`...`** 打开 **「桌面宠物设置」**，或随时按 **`⌥ + ⌘ + P`**（`Option + Command + P`）显隐桌宠。

---

### 🌐 本地 HTTP 控制接口 (`http://127.0.0.1:47821`)

| 接口 | 方法 | 说明 |
| :--- | :---: | :--- |
| `/health` | `GET` | 返回运行状态、多语言设置、全量配置、统计数据、最近会话列表与宠物列表 |
| `/state` | `POST` | 更新指定 `conversationId` 的任务状态（`idle`/`running`/`needs_input`/`ready`/`blocked`） |
| `/skin` | `POST` | 切换宠物角色皮肤（如 `{"skin": "cyber-fox"}`） |
| `/reload` | `POST` | 热重载 `pets/` 与 `%USERPROFILE%\.antigravity-pet\pets/` 宠物包 |
| `/control` | `POST` | 执行窗口与配置控制（`show`/`hide`/`toggle`/`open`/`quit`/`language`/`scale`/`bubbleMode`/`updateConfig`） |

---

### ❓ 常见问题 (FAQ)

- **Q：为什么 Windows 任务栏或 macOS Dock 栏里看不到桌宠？**
  - A：这是特意设计的纯净悬浮模式（Windows 使用 `WS_EX_TOOLWINDOW`，macOS 使用 `LSUIElement` + `.accessory`），避免占用任务栏、托盘与 Dock 空间。你可以通过右键桌宠、快捷键 `Win + Alt + P`（macOS 为 `⌥ + ⌘ + P`）或 Antigravity 右上角 `...` 的 **「桌面宠物设置」** 随时管理。
- **Q：如何关闭开机随 Windows 启动，仅在使用 Antigravity 时运行？**
  - A：新版已彻底移除 Windows 开机自启注册表项，默认即为 **「随 Antigravity 启动」**（打开客户端时拉起、关闭客户端时自动退出）。
- **Q：支持自定义生成新宠物吗？**
  - A：支持！直接在对话中对 Antigravity 说「帮我孵化一只赛博小熊猫桌面宠物」，内置 `hatch-pet` 技能即可为你生成完整 `8×11` 图集与 `pet.json` 并热切换。

---

### 📄 开源协议

本项目基于 [MIT License](LICENSE) 开源。
