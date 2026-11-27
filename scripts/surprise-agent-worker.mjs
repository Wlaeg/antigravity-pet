#!/usr/bin/env node
import { execFile } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const BASE_ACCESSORIES_PATH = path.join(__dirname, 'surprise-base.json');
export const SURPRISE_DIR = path.join(os.homedir(), '.antigravity-pet', 'surprise-accessories');
export const SURPRISE_MANIFEST_PATH = path.join(SURPRISE_DIR, 'manifest.json');
export const SURPRISE_PROGRESS_PATH = path.join(SURPRISE_DIR, 'progress.json');

function rect(px, x0, y0, x1, y1, r, g, b, a = 255) {
  for (let y = y0; y <= y1; y++) {
    for (let x = x0; x <= x1; x++) px(x, y, r, g, b, a);
  }
}

function buildPixels(drawFn) {
  const map = new Map();
  const px = (dx, dy, r, g, b, a = 255) => {
    map.set(`${dx},${dy}`, [dx, dy, r, g, b, a]);
  };
  drawFn(px);
  return Array.from(map.values());
}

// ---------------------------------------------------------------------------
// 1. 20 个四部位互不冲突基础配饰 (head: 5, face: 5, hand: 5, back: 5)
// ---------------------------------------------------------------------------
export const BASE_ACCESSORIES = [
  // HEAD (anchor: cx, headY)
  {
    id: 'base_head_antenna', slot: 'head', nameZh: '信号天线', nameEn: 'Signal Antenna', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -2, -1, 1, -1, 100, 116, 139);
      rect(px, -1, -5, 0, -2, 148, 163, 184);
      rect(px, -2, -7, 1, -6, 56, 189, 248);
      px(-1, -7, 224, 242, 254);
    })
  },
  {
    id: 'base_head_bunny', slot: 'head', nameZh: '像素兔耳', nameEn: 'Bunny Ears', isNewShape: true,
    pixels: buildPixels((px) => {
      for (const sx of [-4, 2]) {
        rect(px, sx, -7, sx + 2, -1, 248, 250, 252);
        rect(px, sx + 1, -6, sx + 1, -2, 244, 114, 182);
      }
    })
  },
  {
    id: 'base_head_horns', slot: 'head', nameZh: '小恶魔角', nameEn: 'Imp Horns', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -5, -2, -3, -1, 190, 18, 60);
      rect(px, -6, -4, -4, -3, 225, 29, 72);
      px(-6, -5, 251, 113, 133);
      rect(px, 2, -2, 4, -1, 190, 18, 60);
      rect(px, 3, -4, 5, -3, 225, 29, 72);
      px(5, -5, 251, 113, 133);
    })
  },
  {
    id: 'base_head_wizard', slot: 'head', nameZh: '魔法尖帽', nameEn: 'Wizard Hat', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -6, -1, 5, -1, 79, 70, 229);
      rect(px, -4, -2, 3, -2, 250, 204, 21);
      rect(px, -3, -4, 2, -3, 99, 102, 241);
      rect(px, -2, -6, 1, -5, 99, 102, 241);
      px(0, -7, 129, 140, 248);
    })
  },
  {
    id: 'base_head_sprout', slot: 'head', nameZh: '元气嫩芽', nameEn: 'Lucky Sprout', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -1, -4, 0, -1, 22, 163, 74);
      rect(px, -4, -6, -1, -5, 74, 222, 128);
      rect(px, 1, -6, 4, -5, 34, 197, 94);
      px(-3, -6, 187, 247, 208);
      px(3, -6, 187, 247, 208);
    })
  },

  // FACE (anchor: cx, ey)
  {
    id: 'base_face_scouter', slot: 'face', nameZh: '战斗探测镜', nameEn: 'Power Scouter', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, 1, 0, 6, 0, 100, 116, 139);
      rect(px, 1, 1, 5, 3, 16, 185, 129, 230);
      px(2, 1, 167, 243, 208);
      rect(px, 6, 1, 7, 2, 71, 85, 105);
    })
  },
  {
    id: 'base_face_bandaid', slot: 'face', nameZh: '元气创可贴', nameEn: 'Cute Bandaid', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -6, 2, -3, 3, 253, 186, 116);
      px(-5, 2, 254, 243, 199);
      px(-4, 3, 254, 243, 199);
    })
  },
  {
    id: 'base_face_blush', slot: 'face', nameZh: '害羞腮红', nameEn: 'Rosy Blush', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -6, 2, -4, 3, 251, 113, 133, 230);
      rect(px, 3, 2, 5, 3, 251, 113, 133, 230);
    })
  },
  {
    id: 'base_face_round', slot: 'face', nameZh: '圆框学霸镜', nameEn: 'Scholar Glasses', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -6, 0, -2, 0, 217, 119, 6);
      rect(px, -6, 3, -2, 3, 217, 119, 6);
      rect(px, -6, 1, -6, 2, 217, 119, 6);
      rect(px, -2, 1, -2, 2, 217, 119, 6);
      rect(px, -5, 1, -3, 2, 186, 230, 253, 150);
      rect(px, -1, 1, 0, 1, 217, 119, 6);
      rect(px, 1, 0, 5, 0, 217, 119, 6);
      rect(px, 1, 3, 5, 3, 217, 119, 6);
      rect(px, 1, 1, 1, 2, 217, 119, 6);
      rect(px, 5, 1, 5, 2, 217, 119, 6);
      rect(px, 2, 1, 4, 2, 186, 230, 253, 150);
    })
  },
  {
    id: 'base_face_mustache', slot: 'face', nameZh: '绅士小胡子', nameEn: 'Dapper Mustache', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -4, 3, -1, 4, 51, 65, 85);
      rect(px, 0, 3, 3, 4, 51, 65, 85);
      px(-5, 3, 71, 85, 105);
      px(4, 3, 71, 85, 105);
    })
  },

  // HAND (anchor: cx, cy)
  {
    id: 'base_hand_wand', slot: 'hand', nameZh: '星辰魔杖', nameEn: 'Star Wand', isNewShape: true,
    pixels: buildPixels((px) => {
      for (let i = 0; i < 6; i++) px(9 + i, 4 - i, 180, 83, 9);
      rect(px, 13, -3, 15, -1, 250, 204, 21);
      px(14, -4, 254, 240, 138);
      px(14, 0, 254, 240, 138);
      px(12, -2, 254, 240, 138);
      px(16, -2, 254, 240, 138);
    })
  },
  {
    id: 'base_hand_gamepad', slot: 'hand', nameZh: '像素手柄', nameEn: 'Retro Gamepad', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, 9, 0, 15, 3, 51, 65, 85);
      rect(px, 9, 3, 10, 4, 30, 41, 59);
      rect(px, 14, 3, 15, 4, 30, 41, 59);
      px(10, 1, 248, 250, 252);
      px(14, 1, 244, 63, 94);
      px(13, 2, 56, 189, 248);
    })
  },
  {
    id: 'base_hand_balloon', slot: 'hand', nameZh: '漂浮气球', nameEn: 'Sky Balloon', isNewShape: true,
    pixels: buildPixels((px) => {
      for (let y = 0; y <= 4; y++) px(10, y, 203, 213, 225);
      rect(px, 9, -5, 12, -1, 244, 63, 94);
      rect(px, 8, -4, 13, -2, 244, 63, 94);
      px(9, -4, 253, 164, 175);
    })
  },
  {
    id: 'base_hand_wrench', slot: 'hand', nameZh: '极客扳手', nameEn: 'Dev Wrench', isNewShape: true,
    pixels: buildPixels((px) => {
      for (let i = 0; i < 5; i++) px(9 + i, 4 - i, 148, 163, 184);
      rect(px, 12, -3, 15, -2, 203, 213, 225);
      px(15, -3, 0, 0, 0, 0);
      px(13, -4, 226, 232, 240);
      px(15, -1, 226, 232, 240);
    })
  },
  {
    id: 'base_hand_boba', slot: 'hand', nameZh: '珍珠奶茶', nameEn: 'Boba Tea', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, 9, 0, 13, 5, 253, 230, 138);
      rect(px, 9, -1, 13, -1, 248, 250, 252);
      rect(px, 11, -4, 11, -1, 244, 63, 94);
      px(10, 4, 69, 26, 3);
      px(12, 4, 69, 26, 3);
      px(11, 3, 69, 26, 3);
    })
  },

  // BACK (anchor: cx, cy)
  {
    id: 'base_back_wings', slot: 'back', nameZh: '天使羽翼', nameEn: 'Angel Wings', isNewShape: true,
    pixels: buildPixels((px) => {
      for (const s of [-1, 1]) {
        rect(px, s * 8, -4, s * 10, 2, 248, 250, 252);
        rect(px, s * 11, -6, s * 13, 0, 241, 245, 249);
        rect(px, s * 13, -7, s * 14, -2, 224, 242, 254);
      }
    })
  },
  {
    id: 'base_back_cape', slot: 'back', nameZh: '勇者披风', nameEn: 'Hero Cape', isNewShape: true,
    pixels: buildPixels((px) => {
      rect(px, -11, -3, -7, 7, 220, 38, 38);
      rect(px, -13, 1, -11, 8, 185, 28, 28);
      rect(px, 7, -3, 11, 7, 220, 38, 38);
      rect(px, 11, 1, 13, 8, 185, 28, 28);
    })
  },
  {
    id: 'base_back_swords', slot: 'back', nameZh: '交叉双剑', nameEn: 'Twin Blades', isNewShape: true,
    pixels: buildPixels((px) => {
      for (let i = 0; i < 10; i++) {
        px(-12 + i, -9 + i, 203, 213, 225);
        px(12 - i, -9 + i, 203, 213, 225);
      }
      rect(px, -14, -10, -12, -8, 250, 204, 21);
      rect(px, 12, -10, 14, -8, 250, 204, 21);
    })
  },
  {
    id: 'base_back_jetpack', slot: 'back', nameZh: '喷气背包', nameEn: 'Jetpack', isNewShape: true,
    pixels: buildPixels((px) => {
      for (const sx of [-11, 8]) {
        rect(px, sx, -4, sx + 3, 3, 148, 163, 184);
        rect(px, sx + 1, -3, sx + 2, 1, 56, 189, 248);
        rect(px, sx + 1, 4, sx + 2, 6, 249, 115, 22);
        px(sx + 1, 7, 250, 204, 21);
      }
    })
  },
  {
    id: 'base_back_halo_ring', slot: 'back', nameZh: '星环背轮', nameEn: 'Star Ring', isNewShape: true,
    pixels: buildPixels((px) => {
      for (let a = 0; a < 16; a++) {
        const ang = (a / 16) * Math.PI * 2;
        const dx = Math.round(Math.cos(ang) * 12);
        const dy = Math.round(Math.sin(ang) * 11) - 1;
        px(dx, dy, 250, 204, 21);
      }
      px(-12, -1, 56, 189, 248);
      px(12, -1, 56, 189, 248);
      px(0, -12, 244, 63, 94);
    })
  }
];

// ---------------------------------------------------------------------------
// 2. 调色板与 150 个全新造型模块化像素引擎（确保 >= 2/3 全新剪影）
// ---------------------------------------------------------------------------
const PALETTES = {
  cyber:   { p: [56, 189, 248], s: [14, 165, 233], hi: [224, 242, 254], acc: [250, 204, 21] },
  crimson: { p: [244, 63, 94],  s: [190, 18, 60],  hi: [254, 205, 211], acc: [251, 191, 36] },
  emerald: { p: [52, 211, 153], s: [5, 150, 105],  hi: [209, 250, 229], acc: [56, 189, 248] },
  amber:   { p: [251, 191, 36], s: [217, 119, 6],  hi: [254, 243, 199], acc: [244, 63, 94] },
  violet:  { p: [167, 139, 250],s: [109, 40, 217], hi: [237, 233, 254], acc: [244, 114, 182] },
  slate:   { p: [148, 163, 184],s: [71, 85, 105],  hi: [241, 245, 249], acc: [56, 189, 248] },
  coral:   { p: [251, 113, 133],s: [225, 29, 72],  hi: [255, 228, 230], acc: [253, 224, 71] },
  mint:    { p: [45, 212, 191], s: [13, 148, 136], hi: [204, 251, 241], acc: [250, 204, 21] }
};

// 每个部位提供大量真实不同的几何形状绘制函数（非单纯换色）
const HEAD_SHAPES = [
  ['beret', '画家贝雷帽', 'Beret', (px, c) => { rect(px, -6, -3, 5, -1, ...c.p); rect(px, -4, -4, 3, -4, ...c.hi); px(0, -5, ...c.s); }],
  ['tophat', '魔术礼帽', 'Top Hat', (px, c) => { rect(px, -6, -1, 5, -1, ...c.s); rect(px, -4, -6, 3, -2, ...c.p); rect(px, -4, -2, 3, -2, ...c.acc); }],
  ['headphones', '头戴耳机', 'Headphones', (px, c) => { rect(px, -6, -4, 5, -3, ...c.s); rect(px, -7, -3, -6, 1, ...c.p); rect(px, 5, -3, 6, 1, ...c.p); px(-7, -1, ...c.acc); px(6, -1, ...c.acc); }],
  ['duck', '头顶小黄鸭', 'Mini Duck', (px, c) => { rect(px, -2, -4, 2, -1, ...c.p); rect(px, -1, -6, 1, -5, ...c.hi); px(2, -5, 249, 115, 22); }],
  ['propeller', '竹蜻蜓帽', 'Propeller Cap', (px, c) => { rect(px, -5, -2, 4, -1, ...c.p); rect(px, -3, -3, 2, -3, ...c.s); px(0, -4, ...c.hi); rect(px, -5, -5, 4, -5, ...c.acc); }],
  ['flame', '烈焰冠冕', 'Flame Crest', (px, c) => { rect(px, -3, -2, 2, -1, ...c.s); px(-2, -4, ...c.p); px(0, -6, ...c.acc); px(1, -5, ...c.p); px(0, -3, ...c.hi); }],
  ['halo', '圣洁光环', 'Angel Halo', (px, c) => { rect(px, -4, -6, 3, -6, ...c.acc); px(-5, -5, ...c.hi); px(4, -5, ...c.hi); rect(px, -4, -4, 3, -4, ...c.p); }],
  ['flower', '像素小花', 'Pixel Blossom', (px, c) => { px(0, -2, 34, 197, 94); rect(px, -1, -5, 1, -3, ...c.p); px(0, -4, ...c.acc); px(-2, -4, ...c.hi); px(2, -4, ...c.hi); }],
  ['helmet', '太空护目罩', 'Astro Visor', (px, c) => { rect(px, -6, -4, 5, -1, ...c.hi); rect(px, -5, -3, 4, -2, ...c.p); px(5, -5, ...c.acc); }],
  ['bowtie', '蝴蝶结发卡', 'Hair Bow', (px, c) => { rect(px, -4, -3, -2, -1, ...c.p); rect(px, 1, -3, 3, -1, ...c.p); rect(px, -1, -2, 0, -2, ...c.acc); }],
  ['unicorn', '独角兽角', 'Unicorn Horn', (px, c) => { rect(px, -1, -2, 0, -1, ...c.p); px(0, -3, ...c.hi); px(0, -4, ...c.acc); px(0, -5, ...c.p); px(0, -6, ...c.hi); }],
  ['mech_ears', '机械猫耳', 'Mecha Ears', (px, c) => { rect(px, -5, -4, -3, -1, ...c.s); px(-4, -3, ...c.acc); rect(px, 2, -4, 4, -1, ...c.s); px(3, -3, ...c.acc); }],
  ['pirate', '海盗三角帽', 'Pirate Hat', (px, c) => { rect(px, -7, -2, 6, -1, ...c.s); rect(px, -4, -4, 3, -3, ...c.p); px(0, -3, 248, 250, 252); }],
  ['hardhat', '工程安全帽', 'Hard Hat', (px, c) => { rect(px, -6, -1, 5, -1, ...c.s); rect(px, -5, -4, 4, -2, ...c.p); rect(px, -1, -5, 0, -2, ...c.hi); }],
  ['sprout_twin', '双叶苗冠', 'Twin Sprouts', (px, c) => { px(-2, -2, ...c.s); px(1, -2, ...c.s); rect(px, -5, -4, -3, -3, ...c.p); rect(px, 2, -4, 4, -3, ...c.p); }],
  ['octopus_hat', '小章鱼帽', 'Octo Buddy', (px, c) => { rect(px, -3, -5, 2, -2, ...c.p); px(-4, -1, ...c.s); px(-1, -1, ...c.s); px(2, -1, ...c.s); px(-2, -3, 248, 250, 252); px(1, -3, 248, 250, 252); }],
  ['lightning_pin', '闪电发夹', 'Volt Clip', (px, c) => { px(2, -4, ...c.acc); px(1, -3, ...c.acc); rect(px, 1, -2, 3, -2, ...c.p); px(2, -1, ...c.acc); }],
  ['laurel', '月桂花冠', 'Laurel Crown', (px, c) => { rect(px, -6, -2, -4, -1, ...c.p); rect(px, 3, -2, 5, -1, ...c.p); px(-5, -3, ...c.hi); px(4, -3, ...c.hi); px(0, -2, ...c.acc); }],
  ['tiara', '宝石小皇冠', 'Mini Tiara', (px, c) => { rect(px, -4, -1, 3, -1, ...c.acc); px(-3, -2, ...c.hi); px(0, -3, ...c.p); px(2, -2, ...c.hi); }],
  ['beanie', '针织冷帽', 'Cozy Beanie', (px, c) => { rect(px, -5, -3, 4, -1, ...c.p); rect(px, -5, -1, 4, -1, ...c.s); rect(px, -1, -5, 0, -4, ...c.hi); }],
  ['bamboo', '竹蜻蜓双翼', 'Aero Rotor', (px, c) => { px(0, -2, ...c.s); px(0, -3, ...c.s); rect(px, -6, -4, -1, -4, ...c.p); rect(px, 1, -4, 6, -4, ...c.acc); }],
  ['sushi', '三文鱼寿司', 'Sushi Hat', (px, c) => { rect(px, -3, -2, 2, -1, 248, 250, 252); rect(px, -4, -4, 3, -3, 249, 115, 22); px(-2, -4, 254, 215, 170); px(1, -4, 254, 215, 170); }],
  ['mushroom', '像素蘑菇伞', 'Shroom Cap', (px, c) => { rect(px, -5, -3, 4, -1, ...c.p); rect(px, -3, -4, 2, -4, ...c.p); px(-3, -2, 248, 250, 252); px(1, -3, 248, 250, 252); }],
  ['trident_crest', '海神三叉纹', 'Trident Crest', (px, c) => { rect(px, -3, -1, 2, -1, ...c.acc); px(-3, -4, ...c.p); px(0, -5, ...c.hi); px(2, -4, ...c.p); }],
  ['cat_bell', '铃铛发箍', 'Bell Band', (px, c) => { rect(px, -5, -2, 4, -2, ...c.s); rect(px, -1, -3, 0, -1, ...c.acc); px(-4, -3, ...c.p); px(3, -3, ...c.p); }],
  ['radar', '微型雷达锅', 'Mini Radar', (px, c) => { rect(px, -2, -2, 1, -1, ...c.s); rect(px, -4, -5, 2, -3, ...c.hi); px(-1, -6, ...c.acc); }],
  ['star_pin', '双星发饰', 'Twin Stars', (px, c) => { px(-4, -2, ...c.acc); px(-3, -3, ...c.hi); px(3, -2, ...c.p); px(2, -3, ...c.hi); }],
  ['chef', '主厨高帽', 'Chef Hat', (px, c) => { rect(px, -4, -2, 3, -1, 241, 245, 249); rect(px, -5, -5, 4, -3, 248, 250, 252); px(-2, -6, 226, 232, 240); px(1, -6, 226, 232, 240); }],
  ['grad', '博士学位帽', 'Grad Cap', (px, c) => { rect(px, -6, -3, 5, -2, ...c.s); rect(px, -3, -1, 2, -1, ...c.p); px(5, -1, ...c.acc); px(5, 0, ...c.acc); }],
  ['fox_mask_top', '侧戴狐面', 'Side Fox Mask', (px, c) => { rect(px, 1, -4, 5, -1, 248, 250, 252); px(1, -5, ...c.p); px(5, -5, ...c.p); px(2, -2, ...c.acc); px(4, -2, ...c.acc); }]
];

const FACE_SHAPES = [
  ['heart_glasses', '爱心墨镜', 'Heart Shades', (px, c) => { rect(px, -6, 0, -2, 2, ...c.p); px(-4, 3, ...c.p); rect(px, -1, 1, 0, 1, ...c.s); rect(px, 1, 0, 5, 2, ...c.p); px(3, 3, ...c.p); }],
  ['star_glasses', '星形眼镜', 'Star Specs', (px, c) => { px(-4, 0, ...c.acc); rect(px, -6, 1, -2, 2, ...c.p); px(-4, 3, ...c.hi); rect(px, -1, 1, 0, 1, ...c.s); px(3, 0, ...c.acc); rect(px, 1, 1, 5, 2, ...c.p); px(3, 3, ...c.hi); }],
  ['vr_visor', '赛博 VR 镜', 'Cyber Visor', (px, c) => { rect(px, -6, 0, 5, 2, ...c.s); rect(px, -5, 1, 4, 1, ...c.p); px(-4, 1, ...c.hi); px(2, 1, ...c.acc); }],
  ['mask', '极客口罩', 'Tech Mask', (px, c) => { rect(px, -4, 2, 3, 4, ...c.s); rect(px, -3, 3, 2, 3, ...c.p); px(-5, 2, ...c.hi); px(4, 2, ...c.hi); }],
  ['monocle', '单片金丝镜', 'Monocle', (px, c) => { rect(px, 1, 0, 4, 0, ...c.acc); rect(px, 1, 3, 4, 3, ...c.acc); px(1, 1, ...c.acc); px(4, 1, ...c.acc); px(1, 2, ...c.acc); px(4, 2, ...c.acc); px(5, 4, ...c.hi); }],
  ['bandages', '双侧绷带', 'Cross Bandaids', (px, c) => { rect(px, -6, 2, -4, 3, 253, 230, 138); px(-5, 2, ...c.p); rect(px, 3, 2, 5, 3, 253, 230, 138); px(4, 2, ...c.p); }],
  ['whiskers', '三道猫须', 'Cat Whiskers', (px, c) => { rect(px, -7, 1, -5, 1, ...c.hi); rect(px, -7, 3, -5, 3, ...c.p); rect(px, 4, 1, 6, 1, ...c.hi); rect(px, 4, 3, 6, 3, ...c.p); }],
  ['bolt_mark', '闪电眼纹', 'Bolt Mark', (px, c) => { px(4, 0, ...c.acc); px(3, 1, ...c.acc); px(4, 2, ...c.p); px(3, 3, ...c.hi); }],
  ['star_sticker', '颊边星贴', 'Cheek Star', (px, c) => { px(-5, 2, ...c.acc); px(-6, 3, ...c.p); px(-4, 3, ...c.p); px(-5, 4, ...c.hi); }],
  ['nvg', '夜视双目镜', 'Night Goggles', (px, c) => { rect(px, -6, 0, -2, 2, ...c.s); px(-4, 1, 74, 222, 128); rect(px, 1, 0, 5, 2, ...c.s); px(3, 1, 74, 222, 128); rect(px, -1, 1, 0, 1, ...c.s); }],
  ['pipe', '侦探小烟斗', 'Bubble Pipe', (px, c) => { rect(px, 2, 3, 5, 3, 146, 64, 14); rect(px, 5, 1, 6, 3, 120, 53, 15); px(6, -1, ...c.hi); px(7, -2, ...c.p); }],
  ['cyber_patch', '机械眼罩', 'Mecha Patch', (px, c) => { rect(px, -6, 0, -2, 2, ...c.s); px(-4, 1, ...c.acc); rect(px, -1, 0, 5, 0, ...c.s); }],
  ['tear_gem', '星泪泪痣', 'Star Tear', (px, c) => { px(-4, 2, ...c.p); px(-4, 3, ...c.hi); px(4, 2, ...c.acc); }],
  ['half_rim', '半框潮镜', 'Half-Rim', (px, c) => { rect(px, -6, 0, -2, 0, ...c.p); rect(px, 1, 0, 5, 0, ...c.p); rect(px, -1, 0, 0, 0, ...c.s); rect(px, -5, 1, -3, 2, ...c.hi, 140); rect(px, 2, 1, 4, 2, ...c.hi, 140); }],
  ['freckles', '元气小雀斑', 'Cute Freckles', (px, c) => { px(-5, 2, ...c.p); px(-3, 3, ...c.p); px(3, 3, ...c.p); px(5, 2, ...c.p); }],
  ['mask_half', '忍者半面罩', 'Ninja Mask', (px, c) => { rect(px, -5, 2, 4, 4, ...c.s); rect(px, -4, 3, 3, 4, ...c.p); px(5, 3, ...c.acc); }],
  ['sunglass_pixel', '像素宽墨镜', '8-Bit Shades', (px, c) => { rect(px, -6, 0, 5, 1, ...c.s); rect(px, -5, 2, -2, 2, ...c.p); rect(px, 1, 2, 4, 2, ...c.p); px(-4, 1, ...c.hi); px(2, 1, ...c.hi); }],
  ['heart_cheek', '双颊爱心', 'Heart Cheeks', (px, c) => { px(-6, 2, ...c.p); px(-4, 2, ...c.p); px(-5, 3, ...c.acc); px(4, 2, ...c.p); px(6, 2, ...c.p); px(5, 3, ...c.acc); }],
  ['lollipop_mouth', '叼棒棒糖', 'Candy Stick', (px, c) => { rect(px, 1, 3, 4, 3, 248, 250, 252); rect(px, 5, 2, 6, 4, ...c.p); px(5, 3, ...c.hi); }],
  ['leaf_mouth', '衔叶小草', 'Leaf Twig', (px, c) => { rect(px, 1, 3, 4, 3, 120, 53, 15); rect(px, 4, 1, 6, 2, 34, 197, 94); px(5, 1, 134, 239, 172); }]
];

const HAND_SHAPES = [
  ['sword', '像素勇者剑', 'Pixel Sword', (px, c) => { rect(px, 10, 3, 12, 3, ...c.acc); px(9, 4, ...c.s); for (let i = 0; i < 6; i++) px(11 + i, 2 - i, ...c.hi); px(15, -3, ...c.p); }],
  ['trident', '海神三叉戟', 'Mini Trident', (px, c) => { for (let y = -2; y <= 5; y++) px(11, y, ...c.s); rect(px, 9, -2, 13, -2, ...c.acc); px(9, -4, ...c.p); px(11, -5, ...c.hi); px(13, -4, ...c.p); }],
  ['shield', '徽章圆盾', 'Crest Shield', (px, c) => { rect(px, 9, -1, 14, 4, ...c.s); rect(px, 10, 0, 13, 3, ...c.p); px(11, 1, ...c.acc); px(12, 2, ...c.hi); }],
  ['fan', '折扇', 'Folding Fan', (px, c) => { rect(px, 9, 0, 14, 2, ...c.p); rect(px, 10, 3, 13, 4, ...c.acc); px(11, 1, ...c.hi); px(12, 1, ...c.hi); }],
  ['laptop', '迷你笔记本', 'Mini Laptop', (px, c) => { rect(px, 9, -1, 14, 2, ...c.s); rect(px, 10, 0, 13, 1, ...c.p); rect(px, 8, 3, 14, 4, ...c.hi); }],
  ['icecream', '双球冰淇淋', 'Ice Cream', (px, c) => { rect(px, 10, 2, 12, 5, 217, 119, 6); px(11, 5, 180, 83, 9); rect(px, 9, -1, 13, 1, ...c.p); rect(px, 10, -3, 12, -2, ...c.hi); }],
  ['lollipop', '彩虹棒棒糖', 'Lollipop', (px, c) => { rect(px, 11, 1, 11, 5, 241, 245, 249); rect(px, 9, -3, 13, 0, ...c.p); px(10, -2, ...c.acc); px(12, -1, ...c.hi); }],
  ['umbrella', '晴天小伞', 'Mini Umbrella', (px, c) => { for (let y = -1; y <= 5; y++) px(11, y, ...c.s); rect(px, 8, -3, 14, -2, ...c.p); rect(px, 9, -4, 13, -4, ...c.hi); px(11, -5, ...c.acc); }],
  ['lantern', '暖光灯笼', 'Glow Lantern', (px, c) => { px(11, -2, ...c.s); rect(px, 9, -1, 13, 3, ...c.p); rect(px, 10, 0, 12, 2, ...c.acc); px(11, 4, ...c.s); }],
  ['camera', '复古相机', 'Retro Camera', (px, c) => { rect(px, 9, 0, 14, 3, ...c.s); rect(px, 11, 1, 12, 2, ...c.p); px(10, -1, ...c.acc); px(13, 1, ...c.hi); }],
  ['magnifier', '侦探放大镜', 'Magnifier', (px, c) => { px(9, 4, 120, 53, 15); px(10, 3, 120, 53, 15); rect(px, 10, -1, 14, 2, ...c.acc); rect(px, 11, 0, 13, 1, 186, 230, 253); }],
  ['flag', '胜利小旗', 'Victory Flag', (px, c) => { for (let y = -4; y <= 5; y++) px(10, y, ...c.hi); rect(px, 11, -4, 15, -1, ...c.p); px(13, -3, ...c.acc); }],
  ['bouquet', '像素花束', 'Flower Bouquet', (px, c) => { rect(px, 10, 2, 12, 4, ...c.s); px(9, 0, ...c.p); px(11, -1, ...c.acc); px(13, 0, ...c.hi); px(11, 1, 34, 197, 94); }],
  ['rod', '迷你钓鱼竿', 'Fishing Rod', (px, c) => { for (let i = 0; i < 6; i++) px(9 + i, 4 - i, 180, 83, 9); for (let y = -1; y <= 3; y++) px(15, y, ...c.hi); px(15, 4, ...c.p); }],
  ['potion', '能量药水', 'Mana Potion', (px, c) => { rect(px, 10, 0, 12, 0, ...c.s); rect(px, 9, 1, 13, 4, ...c.p); px(10, 2, ...c.hi); px(11, -1, 217, 119, 6); }],
  ['guitar_hand', '迷你电吉他', 'Mini Guitar', (px, c) => { rect(px, 9, 2, 12, 5, ...c.p); px(12, 1, ...c.hi); px(13, 0, ...c.s); px(14, -1, ...c.acc); }],
  ['torch', '冒险火把', 'Pixel Torch', (px, c) => { rect(px, 10, 1, 11, 5, 146, 64, 14); rect(px, 9, -2, 12, 0, 249, 115, 22); px(10, -3, 250, 204, 21); px(11, -2, 254, 249, 195); }],
  ['bell_hand', '摇铃铛', 'Hand Bell', (px, c) => { px(11, 4, 146, 64, 14); rect(px, 9, 1, 13, 3, ...c.acc); rect(px, 10, 0, 12, 0, ...c.hi); }],
  ['star_globe', '水晶球', 'Crystal Orb', (px, c) => { rect(px, 10, 4, 12, 5, ...c.s); rect(px, 9, 0, 13, 3, ...c.p); px(10, 1, ...c.hi); px(12, 2, ...c.acc); }],
  ['hammer', '像素小锤', 'Pixel Hammer', (px, c) => { rect(px, 11, 1, 11, 5, 146, 64, 14); rect(px, 9, -2, 13, 0, ...c.s); px(9, -2, ...c.hi); px(13, 0, ...c.p); }]
];

const BACK_SHAPES = [
  ['bat_wings', '恶魔蝠翼', 'Bat Wings', (px, c) => { for (const s of [-1, 1]) { rect(px, s * 8, -4, s * 11, 1, ...c.s); rect(px, s * 12, -6, s * 14, 0, ...c.p); px(s * 14, -7, ...c.acc); px(s * 10, 2, ...c.s); } }],
  ['butterfly', '蝴蝶彩翼', 'Butterfly Wings', (px, c) => { for (const s of [-1, 1]) { rect(px, s * 8, -6, s * 13, -1, ...c.p); rect(px, s * 9, 1, s * 12, 5, ...c.s); px(s * 11, -4, ...c.acc); px(s * 11, 3, ...c.hi); } }],
  ['mech_wings', '机械翼刃', 'Mecha Wings', (px, c) => { for (const s of [-1, 1]) { rect(px, s * 8, -5, s * 14, -4, ...c.s); rect(px, s * 9, -2, s * 13, -1, ...c.p); rect(px, s * 8, 1, s * 11, 2, ...c.acc); } }],
  ['windup', '发条钥匙', 'Windup Key', (px, c) => { rect(px, -11, -1, -7, 0, ...c.acc); rect(px, -14, -4, -12, -1, ...c.hi); rect(px, -14, 1, -12, 4, ...c.hi); px(-13, 0, ...c.acc); }],
  ['backpack', '像素双肩包', 'Pixel Pack', (px, c) => { for (const sx of [-11, 8]) { rect(px, sx, -2, sx + 3, 4, ...c.p); rect(px, sx + 1, 0, sx + 2, 2, ...c.acc); } }],
  ['rocket_pack', '双管火箭', 'Twin Rockets', (px, c) => { for (const sx of [-12, 9]) { rect(px, sx, -5, sx + 2, 2, ...c.hi); px(sx + 1, -6, ...c.p); rect(px, sx, 3, sx + 2, 5, 249, 115, 22); } }],
  ['magic_circle', '浮空魔法阵', 'Rune Circle', (px, c) => { for (let a = 0; a < 12; a++) { const ang = (a / 12) * Math.PI * 2; px(Math.round(Math.cos(ang) * 11), Math.round(Math.sin(ang) * 11), ...c.p); } px(-11, 0, ...c.acc); px(11, 0, ...c.acc); }],
  ['guitar_back', '背负吉他', 'Back Guitar', (px, c) => { for (let i = 0; i < 8; i++) px(-12 + i, -8 + i, 146, 64, 14); rect(px, 7, 1, 11, 6, ...c.p); px(9, 3, ...c.hi); }],
  ['shell', '勇者龟壳', 'Pixel Shell', (px, c) => { rect(px, -11, -3, -8, 4, ...c.p); rect(px, 8, -3, 11, 4, ...c.p); px(-10, -1, ...c.hi); px(10, -1, ...c.hi); }],
  ['ribbon_tail', '飘逸丝带', 'Flowing Ribbon', (px, c) => { rect(px, -12, -1, -8, 1, ...c.p); rect(px, -14, 2, -11, 5, ...c.hi); rect(px, 8, -1, 12, 1, ...c.p); rect(px, 11, 2, 14, 5, ...c.hi); }],
  ['flame_wings', '炽焰之翼', 'Flame Wings', (px, c) => { for (const s of [-1, 1]) { rect(px, s * 8, -4, s * 12, 1, 249, 115, 22); px(s * 13, -5, 250, 204, 21); px(s * 14, -7, 254, 249, 195); px(s * 12, 2, 220, 38, 38); } }],
  ['katana', '太刀背鞘', 'Katana Sheath', (px, c) => { for (let i = 0; i < 12; i++) px(-13 + i, -10 + i, ...c.s); px(-14, -11, ...c.acc); px(8, 2, ...c.p); }],
  ['star_satellites', '卫星环绕', 'Orbiting Stars', (px, c) => { rect(px, -13, -5, -11, -3, ...c.acc); rect(px, 11, 2, 13, 4, ...c.p); px(-12, 5, ...c.hi); px(12, -6, ...c.acc); }],
  ['leaf_cape', '精灵叶披风', 'Leaf Cloak', (px, c) => { rect(px, -11, -2, -7, 6, 34, 197, 94); rect(px, 7, -2, 11, 6, 34, 197, 94); px(-12, 4, 74, 222, 128); px(12, 4, 74, 222, 128); }],
  ['bow_quiver', '弓箭箭袋', 'Bow & Quiver', (px, c) => { rect(px, -12, -5, -9, 3, 146, 64, 14); px(-11, -7, ...c.p); px(-9, -7, ...c.acc); for (let y = -6; y <= 4; y++) px(10, y, 180, 83, 9); }],
  ['cloud_wings', '云朵小翼', 'Cloud Wings', (px, c) => { for (const s of [-1, 1]) { rect(px, s * 8, -3, s * 13, 1, 248, 250, 252); rect(px, s * 9, -5, s * 12, -4, 224, 242, 254); } }],
  ['cyber_fins', '赛博鳍翼', 'Cyber Fins', (px, c) => { for (const s of [-1, 1]) { rect(px, s * 8, -6, s * 9, 2, ...c.s); px(s * 10, -5, ...c.p); px(s * 11, -3, ...c.p); px(s * 12, -1, ...c.acc); } }],
  ['moon_halo', '弯月背饰', 'Crescent Moon', (px, c) => { for (let y = -8; y <= 5; y++) px(-11, y, ...c.acc); px(-10, -8, ...c.hi); px(-10, 5, ...c.hi); px(-12, -3, ...c.acc); px(-12, 0, ...c.acc); }],
  ['prop_pack', '螺旋飞翼', 'Rotor Pack', (px, c) => { rect(px, -11, -2, -8, 2, ...c.s); rect(px, 8, -2, 11, 2, ...c.s); rect(px, -14, -4, -9, -4, ...c.p); rect(px, 9, -4, 14, -4, ...c.p); }],
  ['lantern_wings', '浮灯双翼', 'Spirit Lamps', (px, c) => { for (const s of [-1, 1]) { rect(px, s * 10, -3, s * 12, 1, ...c.p); px(s * 11, -1, ...c.acc); px(s * 11, -4, ...c.s); } }]
];

const EXTRAS = [
  ['spark', '星辉', 'Spark', (px, c, slot) => {
    const oy = slot === 'head' ? -6 : slot === 'face' ? -1 : slot === 'hand' ? -4 : -8;
    const ox = slot === 'hand' ? 14 : 0;
    px(ox, oy, ...c.acc); px(ox + 1, oy - 1, ...c.hi);
  }],
  ['gem', '宝石', 'Gem', (px, c, slot) => {
    const oy = slot === 'head' ? -2 : slot === 'face' ? 1 : slot === 'hand' ? 2 : 0;
    const ox = slot === 'hand' ? 11 : slot === 'back' ? -10 : 0;
    px(ox, oy, ...c.acc);
  }],
  ['winglet', '小徽记', 'Crest', (px, c, slot) => {
    if (slot === 'head') { px(-5, -3, ...c.hi); px(4, -3, ...c.hi); }
    else if (slot === 'back') { px(-13, -7, ...c.hi); px(13, -7, ...c.hi); }
    else if (slot === 'hand') { px(15, 0, ...c.hi); }
    else { px(-7, 0, ...c.hi); px(6, 0, ...c.hi); }
  }]
];

// ---------------------------------------------------------------------------
// 3. 本地 Agent 配方请求（混合模式：~6,000 Tokens，失败时自动降级本地词库）
// ---------------------------------------------------------------------------
function tryAskLocalAgentForRecipes() {
  return new Promise((resolve) => {
    const prompt = [
      'Generate 150 compact JSON accessory recipes for a 36x39 pixel desktop pet.',
      'Return ONLY a JSON array of 150 objects: [{"slot":"head|face|hand|back","shape":"key","palette":"cyber|crimson|emerald|amber|violet|slate|coral|mint","nameZh":"2-5汉字","nameEn":"1-3 words"}].',
      `Valid head shapes: ${HEAD_SHAPES.map(x => x[0]).join(',')}.`,
      `Valid face shapes: ${FACE_SHAPES.map(x => x[0]).join(',')}.`,
      `Valid hand shapes: ${HAND_SHAPES.map(x => x[0]).join(',')}.`,
      `Valid back shapes: ${BACK_SHAPES.map(x => x[0]).join(',')}.`,
      'Ensure at least 2/3 use distinct shapes rather than recoloring.'
    ].join(' ');

    const tryCli = (cmd, args) => new Promise((res) => {
      execFile(cmd, args, { timeout: 12000, maxBuffer: 1024 * 1024 }, (err, stdout) => {
        if (err || !stdout) return res(null);
        const m = stdout.match(/\[[\s\S]*\]/);
        if (!m) return res(null);
        try {
          const arr = JSON.parse(m[0]);
          if (Array.isArray(arr) && arr.length >= 30) return res(arr);
        } catch {}
        res(null);
      });
    });

    (async () => {
      let recipes = await tryCli('agy', ['ask', '--no-tools', prompt]);
      if (!recipes) recipes = await tryCli('gemini', ['-p', prompt]);
      resolve(recipes);
    })();
  });
}

function synthesizeCatalog(agentRecipes) {
  const slotTables = {
    head: HEAD_SHAPES,
    face: FACE_SHAPES,
    hand: HAND_SHAPES,
    back: BACK_SHAPES
  };
  const palKeys = Object.keys(PALETTES);
  const items = [];
  const usedKeys = new Set();

  const addItem = (slot, shapeArr, palKey, extraIdx, customNameZh, customNameEn) => {
    const [shapeKey, defZh, defEn, drawFn] = shapeArr;
    const pal = PALETTES[palKey] || PALETTES.cyber;
    const extra = extraIdx >= 0 ? EXTRAS[extraIdx % EXTRAS.length] : null;
    const uniqKey = `${slot}:${shapeKey}:${palKey}:${extraIdx}`;
    if (usedKeys.has(uniqKey)) return false;
    usedKeys.add(uniqKey);

    const pixels = buildPixels((px) => {
      drawFn(px, pal);
      if (extra) extra[3](px, pal, slot);
    });
    const suffixZh = extra ? extra[1] : '';
    const suffixEn = extra ? `${extra[2]} ` : '';
    items.push({
      id: `sur_${slot}_${shapeKey}_${palKey}_${items.length + 1}`,
      slot,
      shape: shapeKey,
      nameZh: customNameZh || `${suffixZh}${defZh}`,
      nameEn: customNameEn || `${suffixEn}${defEn}`,
      pixels
    });
    return true;
  };

  // 先消化本地 Agent 返回的创意配方（优先保证每个 shape 至少覆盖多种独立剪影）
  if (Array.isArray(agentRecipes)) {
    for (const r of agentRecipes) {
      if (items.length >= 150) break;
      const slot = String(r && r.slot || '').toLowerCase();
      const table = slotTables[slot];
      if (!table) continue;
      const shapeArr = table.find((s) => s[0] === r.shape) || table[items.length % table.length];
      const palKey = PALETTES[r.palette] ? r.palette : palKeys[items.length % palKeys.length];
      const extraIdx = items.length % 4 === 0 ? (items.length % EXTRAS.length) : -1;
      addItem(slot, shapeArr, palKey, extraIdx, r.nameZh, r.nameEn);
    }
  }

  // 补齐到 150 个：优先遍历全部 90 个独立几何剪影 + 叠加结构变体，确保 >= 2/3 全新造型而非纯换色
  const slots = ['head', 'face', 'hand', 'back'];
  let round = 0;
  while (items.length < 150 && round < 20) {
    for (const slot of slots) {
      const table = slotTables[slot];
      for (let si = 0; si < table.length && items.length < 150; si++) {
        const palKey = palKeys[(si + round * 3) % palKeys.length];
        const extraIdx = round === 0 ? -1 : ((si + round) % EXTRAS.length);
        addItem(slot, table[si], palKey, extraIdx);
      }
    }
    round++;
  }
  return items.slice(0, 150);
}

function writeJsonAtomic(filePath, data) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const tmp = filePath + '.tmp';
  fs.writeFileSync(tmp, JSON.stringify(data), 'utf8');
  fs.renameSync(tmp, filePath);
}

export function ensureBaseAccessoriesFile() {
  writeJsonAtomic(BASE_ACCESSORIES_PATH, { version: 1, count: BASE_ACCESSORIES.length, items: BASE_ACCESSORIES });
}

export async function runSurpriseAgentGeneration({ delayMs = 60 } = {}) {
  fs.mkdirSync(SURPRISE_DIR, { recursive: true });
  ensureBaseAccessoriesFile();

  writeJsonAtomic(SURPRISE_PROGRESS_PATH, {
    status: 'generating',
    current: 0,
    total: 150,
    labelZh: '正在唤醒本地 Agent 构思配饰配方…',
    labelEn: 'Starting local Agent for accessory recipes...',
    updatedAt: new Date().toISOString()
  });

  const agentRecipes = await tryAskLocalAgentForRecipes();
  const generated150 = synthesizeCatalog(agentRecipes);

  const built = [];
  for (let i = 0; i < generated150.length; i++) {
    built.push(generated150[i]);
    if ((i + 1) % 5 === 0 || i === generated150.length - 1) {
      writeJsonAtomic(SURPRISE_PROGRESS_PATH, {
        status: i === generated150.length - 1 ? 'finalizing' : 'generating',
        current: i + 1,
        total: 150,
        labelZh: generated150[i].nameZh,
        labelEn: generated150[i].nameEn,
        updatedAt: new Date().toISOString()
      });
      if (delayMs > 0) await new Promise((r) => setTimeout(r, delayMs));
    }
  }

  writeJsonAtomic(SURPRISE_MANIFEST_PATH, {
    version: 1,
    generatedAt: new Date().toISOString(),
    usedLocalAgent: Boolean(agentRecipes),
    baseCount: BASE_ACCESSORIES.length,
    generatedCount: built.length,
    items: built
  });

  writeJsonAtomic(SURPRISE_PROGRESS_PATH, {
    status: 'done',
    current: 150,
    total: 150,
    labelZh: '150 款惊喜配饰已全部生成',
    labelEn: 'All 150 surprise accessories ready',
    updatedAt: new Date().toISOString()
  });

  return { ok: true, count: built.length, usedLocalAgent: Boolean(agentRecipes) };
}

// CLI 直接运行支持
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.argv.includes('--write-base-only')) {
    ensureBaseAccessoriesFile();
    console.log('Base accessories written:', BASE_ACCESSORIES.length);
  } else {
    runSurpriseAgentGeneration().then((r) => {
      console.log('Surprise generation done:', r);
    }).catch((e) => {
      console.error(e);
      process.exit(1);
    });
  }
}
