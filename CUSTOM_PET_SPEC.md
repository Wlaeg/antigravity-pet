# Custom Pet Pack Specification (v3) / 自定义宠物包规范 (v3)

[English](#english) · [简体中文](#简体中文)

---

<a id="english"></a>

## English

**Antigravity Pet** supports hot-reloading and one-click `.zip` importing of custom pixel-art characters. On Windows & macOS, you can provide a **Sprite Sheet (`1536×2288`)**, **Per-Action Frame Folders**, or even a **Minimal Single Base Image** (`idle.png` / `pet.png` / `avatar.png`). Missing actions automatically fall back to the idle image with procedural squash-and-stretch, parabolic hopping, breathing, and `ScaleX = -1` horizontal flipping.

### 1. Directory Layout

```text
my-custom-pet/
├── pet.json               # Required: manifest metadata (v3)
├── spritesheet.png        # Option A: 8×11 sprite atlas (1536×2288 px)
├── sounds/                # Optional: custom state sound effects (.wav)
│   ├── ready.wav
│   ├── needs_input.wav
│   └── blocked.wav
└── idle/                  # Option B: per-action frame folders (if no spritesheet.png)
    ├── 01.png
    └── 02.png
```

### 2. `pet.json` Manifest (v3)

```json
{
  "id": "my-custom-pet",
  "displayName": "Cyber Red Panda",
  "description": "Custom v3 pixel companion with neon visor",
  "spriteVersionNumber": 3,
  "themeColor": "#F97316",
  "spritesheetPath": "spritesheet.png",
  "sounds": {
    "ready": "sounds/ready.wav",
    "needs_input": "sounds/needs_input.wav",
    "blocked": "sounds/blocked.wav"
  }
}
```

| Field | Required | Type | Description |
| :--- | :---: | :--- | :--- |
| `id` | Yes | string | Unique lowercase identifier (`[a-z0-9-_]`) |
| `displayName` | Yes | string | Name shown in Settings Panel & context menu |
| `description` | No | string | Short character bio |
| `spriteVersionNumber` | Yes | number | Recommended: `3` |
| `themeColor` | No | string | Hex accent color (e.g. `#38BDF8`) for halo & running bubble |
| `spritesheetPath` | No | string | Relative path to atlas PNG (default `spritesheet.png`) |
| `sounds` | No | object | Relative `.wav` paths for `ready`, `needs_input`, `blocked` |

### 3. Sprite Sheet Grid (`1536×2288 px`)

- **Grid**: `8 columns × 11 rows`, each cell is **`192 × 208 px`** (nearest-neighbor rendered; unused trailing cells must be transparent).
- **Recommended logical pixel scale**: `36 × 39` grid at `5×` (`180 × 195 px`) centered inside `192 × 208 px` (`6 px` margin).

| Row | Action / Folder Name | Recommended Frames | Usage |
| :---: | :--- | :---: | :--- |
| **0** | `idle` | 6 | Idle breathing & blink |
| **1** | `walk_right` | 8 | Walking right |
| **2** | `walk_left` | 8 | Walking left |
| **3** | `wave` | 4 | Hover greeting & quick-dispatch cue |
| **4** | `jump` | 5 | Click bounce & drag airborne loop |
| **5** | `blocked` | 8 | Error / blocked dizzy loop |
| **6** | `needs_input` | 6 | Waiting for user input / approval |
| **7** | `running` | 6 | Working animation (typing / custom prop) |
| **8** | `ready` | 6 | Task completion celebration |
| **9–10** | *(look directions)* | 8 + 8 | 16-direction cursor eye-tracking (clockwise from right) |

### 4. How to Import

1. **One-Click `.zip` Import**: Zip the folder containing `pet.json`, open **Desktop Pet Settings** (`...` menu in Antigravity), click **Import .zip**, and select your archive. It will be extracted to `~/.antigravity-pet/pets/<id>/` and activated automatically.
2. **Manual Placement**: Copy the folder into `%USERPROFILE%\.antigravity-pet\pets\<id>\` (Windows) or `~/.antigravity-pet/pets/<id>/` (macOS), then click **Reload** in Settings (or `POST http://127.0.0.1:47821/reload`).

### 5. Community Submission Requirements

To publish your pet to the in-plugin **Community Workshop**, submit a [Community Pet Submission Issue](https://github.com/Wlaeg/antigravity-pet/issues/new?template=pet_submission.yml) with:

1. **Pet Pack `.zip` (Required)**: Valid v3 pack containing `pet.json` and sprite assets.
2. **Full-Body Preview Image (Required)**: Standalone PNG / WebP / JPG full-body image provided by the author (displayed directly in the Community Workshop card before download).
3. **Metadata**: `id`, `displayName`, `description`, `themeColor`, and search `tags`.

---

<a id="简体中文"></a>

## 简体中文

**Antigravity Pet** 支持通过设置面板一键导入 `.zip` 自定义宠物包或热重载本地角色目录，Windows 与 macOS 双端同时兼容 **整图精灵图集（`1536×2288`）**、**分动作序列帧目录** 以及 **极简单张基础图（如 `idle.png` / `pet.png` / `avatar.png`）**。未提供的动作与向左朝向会自动回退至基础待机图，并由程序化动画引擎自动驱动呼吸、抛物线跳跃、果冻回弹与 `ScaleX = -1` 水平翻转。

### 1. 目录结构

```text
my-custom-pet/
├── pet.json               # 必填：v3 角色清单配置
├── spritesheet.png        # 方式 A：8×11 精灵图集（1536×2288 px）
├── sounds/                # 可选：自定义状态提示音（.wav）
│   ├── ready.wav
│   ├── needs_input.wav
│   └── blocked.wav
└── idle/                  # 方式 B：分动作序列帧目录（无 spritesheet.png 时自动回退）
    ├── 01.png
    └── 02.png
```

### 2. `pet.json` 清单规范（v3）

```json
{
  "id": "my-custom-pet",
  "displayName": "赛博小熊猫",
  "description": "带霓虹护目镜的自定义 v3 像素伴侣",
  "spriteVersionNumber": 3,
  "themeColor": "#F97316",
  "spritesheetPath": "spritesheet.png",
  "sounds": {
    "ready": "sounds/ready.wav",
    "needs_input": "sounds/needs_input.wav",
    "blocked": "sounds/blocked.wav"
  }
}
```

| 字段 | 必填 | 类型 | 说明 |
| :--- | :---: | :--- | :--- |
| `id` | 是 | string | 唯一角色标识（仅建议小写字母、数字、`-`、`_`） |
| `displayName` | 是 | string | 设置面板与右键菜单显示的名称 |
| `description` | 否 | string | 角色简介 |
| `spriteVersionNumber` | 是 | number | 推荐设为 `3` |
| `themeColor` | 否 | string | 16 进制主题色（如 `#38BDF8`），用于光环与运行气泡边框 |
| `spritesheetPath` | 否 | string | 精灵图集相对路径（默认 `spritesheet.png`） |
| `sounds` | 否 | object | 自定义 `.wav` 音效映射（支持 `ready`、`needs_input`、`blocked`） |

### 3. 精灵图规格与动作行映射

- **整图尺寸**：`1536 × 2288 px`（`8 列 × 11 行`），单格 **`192 × 208 px`**（严格最近邻无插值缩放，行尾空余单元格请保持全透明）。
- **推荐像素画比例**：`36 × 39` 逻辑像素网格放大 `5` 倍（`180 × 195 px`）居中放置于 `192 × 208 px` 单元格内。

| 行号 | 动作 / 序列帧目录名 | 推荐帧数 | 触发场景 |
| :---: | :--- | :---: | :--- |
| **0** | `idle` | 6 | 待机呼吸与眨眼 |
| **1** | `walk_right` | 8 | 向右散步 |
| **2** | `walk_left` | 8 | 向左散步 |
| **3** | `wave` | 4 | 悬停挥手、双击派活响应 |
| **4** | `jump` | 5 | 点击跳跃、拖拽拎起动画 |
| **5** | `blocked` | 8 | 工具出错 / 阻断晕眩 |
| **6** | `needs_input` | 6 | 等待用户输入或气泡审批 |
| **7** | `running` | 6 | 工作中（敲代码 / 专属抱物动态） |
| **8** | `ready` | 6 | 任务完成庆祝 |
| **9–10** | *(look directions)* | 8 + 8 | 16 方向眼神注视鼠标（从正右起顺时针） |

### 4. 导入与启用方式

1. **面板一键导入 `.zip`**：将包含 `pet.json` 的角色目录打包为 `.zip`，在 Antigravity 右上角 `...` 打开 **「桌面宠物设置」**，点击 **「导入宠物包」** 上传即可自动解压至 `~/.antigravity-pet/pets/<id>/` 并切换启用。
2. **本地目录热重载**：将角色目录放入 `%USERPROFILE%\.antigravity-pet\pets\<id>\`（Windows）或 `~/.antigravity-pet/pets/<id>/`（macOS），在设置面板点击 **「刷新列表」**（或调用 `POST http://127.0.0.1:47821/reload`）即可生效。
3. **脚本参考**：可参考仓库内 [`scripts/generate-default-pets.ps1`](scripts/generate-default-pets.ps1) 生成无锯齿 `1536×2288` 像素精灵图。

### 5. 社区投稿必填规范

如希望将自制宠物上架至插件内 **「社区宠物库」**，请通过 [Community Pet Submission Issue](https://github.com/Wlaeg/antigravity-pet/issues/new?template=pet_submission.yml) 提交：

1. **宠物包 `.zip`（必填）**：符合本规范的压缩包，内含 `pet.json` 与精灵图资源。
2. **宠物全身预览图（必填）**：由作者单独提供的全身预览图（PNG / WebP / JPG），用于在用户下载前直接展示在社区宠物库卡片中。
3. **基础元信息**：`id`、`displayName`、`description`、`themeColor` 与搜索 `tags`。
