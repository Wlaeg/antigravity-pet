---
name: pet
description: >-
  控制 Antigravity 桌面宠物（Codex 兼容版星灵桌宠）。当用户要求打开、召唤、隐藏、关闭桌面宠物，
  打开设置面板，切换宠物皮肤（cyber-fox / astro-bot / shadow-cat / byte-ghost / shiba-dog / pixel-dragon），
  调整尺寸/气泡模式/随 Antigravity 启停/提示音、刷新宠物列表、或查看/测试桌宠状态时使用。
---

# Antigravity 桌面宠物控制指南（v3.0）

- 程序路径：`C:\Users\Nelson\.gemini\config\plugins\antigravity-pet\app\AntigravityPet.exe`
- 宠物包扫描目录：
  1. `C:\Users\Nelson\.gemini\config\plugins\antigravity-pet\pets\`
  2. `%USERPROFILE%\.codex\pets\`（直接兼容 Codex Pets 社区资料包）
- 本地控制端口：`http://127.0.0.1:47821`
- 全局显隐快捷键：**`Win + Alt + P`**（不占 Windows 任务栏与系统托盘图标）
- 客户端内设置面板：已集成至 Antigravity 右上角三个点（`...`）插件目录「桌面宠物设置」（`sidecar://antigravity-pet/panel/`），亦可调用 MCP 工具 `antigravity-pet_pet:pet_settings`。

## 常用操作

1. **启动 / 召唤显示桌宠**（保证显示在用户真实桌面）：
   - 先尝试发送 `show`：
     ```powershell
     Invoke-RestMethod -Uri "http://127.0.0.1:47821/control" -Method Post -Body '{"action":"show"}' -ContentType "application/json" -TimeoutSec 1
     ```
   - 若桌宠尚未运行，可通过交互式计划任务或 `windows-mcp` `App` 工具拉起（切勿直接在隔离终端 `Start-Process`，否则窗口落在隔离桌面）：
     ```powershell
     schtasks /Run /TN "AntigravityPetLauncher"
     ```
     若计划任务未注册，改用 `windows-mcp_windows-mcp` 的 `App` 工具（`mode: "launch_executable"`, `executable: "C:\\Users\\Nelson\\.gemini\\config\\plugins\\antigravity-pet\\app\\AntigravityPet.exe"`）。

2. **切换宠物皮肤**（内置 6 款：`cyber-fox`、`astro-bot`、`shadow-cat`、`byte-ghost`、`shiba-dog`、`pixel-dragon`）：
   ```powershell
   Invoke-RestMethod -Uri "http://127.0.0.1:47821/skin" -Method Post -Body '{"skin":"shiba-dog"}' -ContentType "application/json"
   ```

3. **热重载宠物包列表**：
   ```powershell
   Invoke-RestMethod -Uri "http://127.0.0.1:47821/reload" -Method Post -Body '{}' -ContentType "application/json"
   ```

4. **窗口控制、打开对应对话与设置**：
   - 打开当前/指定对话：`POST /control` `{"action": "open", "value": "<conversationId>"}`
   - 显隐/退出/动作：`POST /control` `{"action": "hide" | "show" | "toggle" | "quit" | "wave" | "jump" | "walk" | "resetPos"}`
   - 尺寸（`0.75` / `1.0` / `1.3` / `1.5`）：`POST /control` `{"action": "scale", "value": "1.3"}`
   - 气泡模式（`always` / `alerts` / `off`）：`POST /control` `{"action": "bubbleMode", "value": "always"}`
   - 开关项：`POST /control` `{"action": "autoWalk" | "eyeFollow" | "topmost" | "autoStartOnBoot" | "soundAlert", "value": "true|false"}`
