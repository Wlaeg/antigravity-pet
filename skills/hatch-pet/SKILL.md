---
name: hatch-pet
description: >-
  孵化自定义 Antigravity 桌面宠物（hatch-pet 技能）。
  当用户想用描述文字或参考图定制一只专属的桌面宠物角色、生成像素雪碧图与 pet.json
  并立即换上时使用。
---

# 孵化自定义桌面宠物（Hatch Pet）

遵循 **Antigravity Pet v2 像素雪碧图规范** 为用户生成并装载专属桌面宠物。

## 宠物包结构与位置

保存到以下任一目录（推荐写入插件目录以便立即加载）：
- `C:\Users\Nelson\.gemini\config\plugins\antigravity-pet\pets\<pet-id>\`
- 或 `%USERPROFILE%\.antigravity-pet\pets\<pet-id>\`

每个宠物包仅需两个文件：

### 1. `pet.json`
```json
{
  "id": "<pet-id>",
  "displayName": "<中文或英文显示名>",
  "description": "<简短介绍>",
  "spriteVersionNumber": 2,
  "spritesheetPath": "spritesheet.png"
}
```

### 2. `spritesheet.png`（8×11 精灵图网格规范）
- 透明背景 PNG（32 位 RGBA）
- **网格尺寸**：`8 列 × 11 行`（共 88 格）
- **单格尺寸**：`192 × 208 px`
- **整图分辨率**：`1536 × 2288 px`
- **行动作映射**（未使用的行尾单元格必须保持全透明）：
  - Row 0: `idle`（待机呼吸 + 眨眼，推荐 6 帧）
  - Row 1: `running-right`（向右小跑，推荐 8 帧）
  - Row 2: `running-left`（向左小跑，推荐 8 帧）
  - Row 3: `waving`（挥手打招呼，推荐 4 帧）
  - Row 4: `jumping`（开心跳跃，推荐 5 帧）
  - Row 5: `failed`（出错晕眩，推荐 8 帧）
  - Row 6: `waiting`（等待输入/感叹，推荐 6 帧）
  - Row 7: `running`（忙碌工作/敲键盘，推荐 6 帧）
  - Row 8: `review`（完成/星星庆祝，推荐 6 帧）
  - Row 9–10: `look-directions`（顺时针 16 个看鼠标方向，第 0 帧朝正右、第 4 帧朝正下、第 8 帧朝正左、第 12 帧朝正上）

## 生成与立即启用流程

1. 可基于用户描述参考 [generate-default-pets.ps1](file:///C:/Users/Nelson/.gemini/config/plugins/antigravity-pet/scripts/generate-default-pets.ps1) 的 `System.Drawing` 24×26 像素画生成器快速构建高质量无锯齿 1536×2288 精灵图（或结合图像生成工具产出后再用脚本按 192×208 网格对齐裁剪）。
2. 写入 `pet.json` 与 `spritesheet.png` 后，通过本地接口热重载并切换到新宠物：
   ```powershell
   Invoke-RestMethod -Uri "http://127.0.0.1:47821/reload" -Method Post -Body '{}' -ContentType "application/json"
   Invoke-RestMethod -Uri "http://127.0.0.1:47821/skin" -Method Post -Body '{"skin":"<pet-id>"}' -ContentType "application/json"
   ```
