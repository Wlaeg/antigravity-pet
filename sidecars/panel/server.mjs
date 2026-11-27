import { execFile, spawn } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import http from 'node:http';
import https from 'node:https';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PLUGIN_ROOT = path.resolve(__dirname, '..', '..');
const IS_WIN = process.platform === 'win32';
const IS_MAC = process.platform === 'darwin';
const EXE_PATH = path.join(PLUGIN_ROOT, 'app', 'AntigravityPet.exe');
const MAC_APP_BUNDLE = path.join(PLUGIN_ROOT, 'app', 'AntigravityPet.app');
const MAC_BIN_PATH = path.join(MAC_APP_BUNDLE, 'Contents', 'MacOS', 'AntigravityPet');
const MAC_BUILD_SH = path.join(PLUGIN_ROOT, 'build.sh');
const HTML_PATH = path.join(__dirname, 'index.html');
const PETS_DIR = path.join(PLUGIN_ROOT, 'pets');
const USER_PETS_DIR = path.join(os.homedir(), '.antigravity-pet', 'pets');
const COMMUNITY_MANIFEST_PATH = path.join(PLUGIN_ROOT, 'community-pets.json');
const COMMUNITY_MANIFEST_URL = 'https://raw.githubusercontent.com/Wlaeg/antigravity-pet/main/community-pets.json';
const COMMUNITY_STATS_PATH = path.join(PLUGIN_ROOT, 'community-stats.json');
const COMMUNITY_STATS_URL = 'https://raw.githubusercontent.com/Wlaeg/antigravity-pet/main/community-stats.json';
const ABACUS_BASE_URL = 'https://abacus.jasoncameron.dev';
const ABACUS_NS = 'wlaeg-antigravity-pet';
const COMMUNITY_STARS_PATH = path.join(os.homedir(), '.antigravity-pet', 'community-stars.json');
const COMMUNITY_DOWNLOADS_PATH = path.join(os.homedir(), '.antigravity-pet', 'community-downloads.json');
const SURPRISE_BASE_PATH = path.join(PLUGIN_ROOT, 'scripts', 'surprise-base.json');
const SURPRISE_WORKER_PATH = path.join(PLUGIN_ROOT, 'scripts', 'surprise-agent-worker.mjs');
const SURPRISE_DIR = path.join(os.homedir(), '.antigravity-pet', 'surprise-accessories');
const SURPRISE_MANIFEST_PATH = path.join(SURPRISE_DIR, 'manifest.json');
const SURPRISE_PROGRESS_PATH = path.join(SURPRISE_DIR, 'progress.json');
const PORT = 47821;
const TASK_NAME = 'AntigravityPetLauncher';

function getConfigPath() {
  let dir;
  if (IS_MAC) {
    dir = path.join(os.homedir(), 'Library', 'Application Support', 'AntigravityPet');
  } else if (IS_WIN) {
    const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local');
    dir = path.join(localAppData, 'AntigravityPet');
  } else {
    dir = path.join(os.homedir(), '.config', 'AntigravityPet');
  }
  fs.mkdirSync(dir, { recursive: true });
  return path.join(dir, 'config.json');
}

function readOfflineConfig() {
  const defaults = {
    Enabled: true,
    Skin: 'cyber-fox',
    Left: -1,
    Top: -1,
    Topmost: true,
    BubbleMode: 'always',
    BubbleStyle: 'simple',
    PinnedConversationId: '',
    ReadyTimeoutSec: 30,
    Scale: 1.0,
    AutoWalk: true,
    EyeFollow: true,
    AutoStartOnBoot: true,
    CheckUpdatesOnStartup: true,
    SoundAlert: false,
    Language: 'auto',
    AgyDetectedLocale: '',
    CompletedTasksCount: 0,
    ToolCallsCount: 0,
    SnapToAgy: false,
    HideOnFullscreen: true,
    Accessory: 'none',
    AccessoriesStarUnlocked: false,
    SurpriseCurrentPair: [],
    SurpriseCountdown: 0,
    SurprisePackGenerated: false
  };
  try {
    const p = getConfigPath();
    if (fs.existsSync(p)) {
      return { ...defaults, ...JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, '')) };
    }
  } catch {
    // ignore
  }
  return defaults;
}

function writeOfflineConfig(patch) {
  try {
    const cur = readOfflineConfig();
    const next = { ...cur, ...patch };
    fs.writeFileSync(getConfigPath(), JSON.stringify(next), 'utf8');
    return next;
  } catch {
    return readOfflineConfig();
  }
}

function normalizeLocaleCode(raw) {
  const s = String(raw || '').trim().toLowerCase().replace(/_/g, '-');
  if (!s || s === 'auto') return '';
  if (s === 'zh-tw' || s === 'zh-hk' || s === 'zh-mo' || s.includes('hant')) return 'zh-tw';
  if (s === 'zh' || s.startsWith('zh-')) return 'zh';
  if (s === 'es-mx' || s === 'es-419') return 'es-mx';
  if (s === 'es' || s.startsWith('es-')) return 'es';
  for (const code of ['fr', 'de', 'ja', 'ko', 'hi', 'th', 'en']) {
    if (s === code || s.startsWith(code + '-')) return code;
  }
  return '';
}

function detectEffectiveLang(cfg) {
  const mode = normalizeLocaleCode(cfg?.Language);
  if (mode) return mode;
  const agy = normalizeLocaleCode(cfg?.AgyDetectedLocale);
  if (agy) return agy;
  try {
    const agyDataDir = IS_MAC
      ? path.join(os.homedir(), 'Library', 'Application Support', 'Antigravity')
      : path.join(process.env.APPDATA || path.join(os.homedir(), 'AppData', 'Roaming'), 'Antigravity');
    const sp = path.join(agyDataDir, 'app_storage.json');
    if (fs.existsSync(sp)) {
      const raw = fs.readFileSync(sp, 'utf8');
      const m = raw.match(/"(?:locale|language)":"([^"]+)"/i);
      const det = m ? normalizeLocaleCode(m[1]) : '';
      if (det) return det;
    }
    const lp = path.join(agyDataDir, 'localization-prefs.json');
    if (fs.existsSync(lp)) {
      const raw = fs.readFileSync(lp, 'utf8');
      if (/"useTw"\s*:\s*true/i.test(raw)) return 'zh-tw';
      if (/chinese/i.test(raw)) return 'zh';
      if (/english/i.test(raw)) return 'en';
    }
  } catch {}
  return 'en';
}

const TW_PET_META = {
  'cyber-fox': { name: '小藍狐 CyberFox', desc: '賽博藍色小狐狸（預設）' },
  'astro-bot': { name: '像素太空人 Astro', desc: '帶發光面罩的迷你太空人' },
  'shadow-cat': { name: '小黑貓 ShadowCat', desc: '金色大眼的靈動黑貓' },
  'byte-ghost': { name: '電子幽靈 ByteGhost', desc: '呆萌漂浮的霓虹小幽靈' },
  'shiba-dog': { name: '像素柴犬 Shiba', desc: '暖黃捲尾的豆豆眉小柴犬' },
  'pixel-dragon': { name: '像素小青龍 Dragon', desc: '長著金角小翅膀的翡翠青龍' },
  'android-bugdroid': { name: '安卓小綠人 Bugdroid', desc: 'Google Android 官方經典綠色小機器人' },
  'flutter-dash': { name: 'Flutter 小藍鳥 Dash', desc: 'Google Flutter & Dart 官方吉祥物小藍鳥' }
};

const EN_PET_META = {
  'cyber-fox': { name: 'CyberFox', desc: 'Cyber blue fox companion (Default)' },
  'astro-bot': { name: 'AstroBot', desc: 'Mini astronaut with glowing visor' },
  'shadow-cat': { name: 'ShadowCat', desc: 'Golden-eyed nimble black cat' },
  'byte-ghost': { name: 'ByteGhost', desc: 'Cute floating neon ghost' },
  'shiba-dog': { name: 'ShibaDog', desc: 'Warm fluffy Shiba Inu' },
  'pixel-dragon': { name: 'PixelDragon', desc: 'Emerald mini dragon with golden horns' },
  'android-bugdroid': { name: 'Android Bugdroid', desc: 'Classic Google Android green robot mascot' },
  'flutter-dash': { name: 'Flutter Dash', desc: 'Official Google Flutter & Dart blue bird mascot' }
};

function scanPetPacks(lang = 'zh') {
  const map = new Map();
  for (const root of [PETS_DIR, USER_PETS_DIR]) {
    const isCustom = root === USER_PETS_DIR;
    try {
      if (!fs.existsSync(root)) continue;
      for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
        if (!entry.isDirectory()) continue;
        const manifest = path.join(root, entry.name, 'pet.json');
        if (!fs.existsSync(manifest)) continue;
        try {
          const meta = JSON.parse(fs.readFileSync(manifest, 'utf8'));
          const id = meta.id || entry.name;
          const enInfo = EN_PET_META[id];
          const twInfo = TW_PET_META[id];
          const useZh = lang === 'zh';
          const useTw = lang === 'zh-tw';
          map.set(id, {
            id,
            displayName: enInfo ? enInfo.name : (meta.displayName || id),
            description: useTw && twInfo ? twInfo.desc : (!useZh && !useTw && enInfo ? enInfo.desc : (meta.description || '')),
            version: meta.spriteVersionNumber || 2,
            isCustom
          });
        } catch {
          // ignore
        }
      }
    } catch {
      // ignore
    }
  }
  return Array.from(map.values()).sort((a, b) => {
    if (Boolean(a.isCustom) !== Boolean(b.isCustom)) return a.isCustom ? 1 : -1;
    return a.displayName.localeCompare(b.displayName, lang === 'en' ? 'en' : 'zh-CN');
  });
}

function httpRequest(method, urlPath, bodyObj = null, timeoutMs = 800) {
  return new Promise((resolve) => {
    const payload = bodyObj ? JSON.stringify(bodyObj) : null;
    const req = http.request(
      {
        hostname: '127.0.0.1',
        port: PORT,
        path: urlPath,
        method,
        headers: payload
          ? {
              'Content-Type': 'application/json; charset=utf-8',
              'Content-Length': Buffer.byteLength(payload)
            }
          : {},
        timeout: timeoutMs
      },
      (res) => {
        let raw = '';
        res.setEncoding('utf8');
        res.on('data', (c) => (raw += c));
        res.on('end', () => {
          try {
            resolve({ ok: res.statusCode === 200, data: JSON.parse(raw) });
          } catch {
            resolve({ ok: res.statusCode === 200, data: null });
          }
        });
      }
    );
    req.on('timeout', () => {
      req.destroy();
      resolve({ ok: false, data: null });
    });
    req.on('error', () => resolve({ ok: false, data: null }));
    if (payload) req.write(payload);
    req.end();
  });
}

function syncRegistryAutoStart() {
  if (!IS_WIN) return Promise.resolve();
  return new Promise((resolve) => {
    const key = 'HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run';
    execFile('reg.exe', ['DELETE', key, '/v', 'AntigravityPet', '/f'], { timeout: 1500, windowsHide: true }, () => resolve());
  });
}

async function ensurePetLaunchedWithAgy() {
  try {
    const cfg = readOfflineConfig();
    if (cfg.Enabled === false || cfg.AutoStartOnBoot === false) return;
    const res = await httpRequest('GET', '/health', null, 500);
    if (!res.ok) {
      await launchPetOnRealDesktop();
    }
  } catch {
    // ignore
  }
}

function launchPetOnRealDesktop() {
  return new Promise((resolve) => {
    if (IS_MAC) {
      if (fs.existsSync(MAC_BIN_PATH)) {
        execFile('open', ['-g', '-a', MAC_APP_BUNDLE], { timeout: 1500 }, (err) => {
          if (err) {
            try {
              const child = spawn(MAC_BIN_PATH, [], { detached: true, stdio: 'ignore' });
              child.unref();
            } catch {}
          }
          resolve();
        });
      } else if (fs.existsSync(MAC_BUILD_SH)) {
        execFile('bash', [MAC_BUILD_SH, '--launch'], { timeout: 30000 }, () => resolve());
      } else {
        resolve();
      }
      return;
    }

    execFile('schtasks', ['/Run', '/TN', TASK_NAME], { timeout: 1500, windowsHide: true }, (err) => {
      if (err) {
        try {
          const child = spawn(EXE_PATH, [], { detached: true, stdio: 'ignore', windowsHide: false });
          child.unref();
        } catch {
          // ignore
        }
      }
      resolve();
    });
  });
}

async function waitForHealth(maxWaitMs = 2800) {
  const start = Date.now();
  while (Date.now() - start < maxWaitMs) {
    const res = await httpRequest('GET', '/health', null, 400);
    if (res.ok && res.data) return res.data;
    await new Promise((r) => setTimeout(r, 250));
  }
  return null;
}

const ACCESSORY_MILESTONES = { glasses: 20, cap: 50, coffee: 100, crown: 150, surprise: 200 };

function loadSurpriseCatalog() {
  const items = [];
  for (const p of [SURPRISE_BASE_PATH, SURPRISE_MANIFEST_PATH]) {
    try {
      if (fs.existsSync(p)) {
        const obj = JSON.parse(fs.readFileSync(p, 'utf8'));
        if (Array.isArray(obj.items)) items.push(...obj.items);
      }
    } catch {}
  }
  return items;
}

function pickTwoDistinctSlotAccessories() {
  const items = loadSurpriseCatalog();
  if (items.length < 2) return [];
  const bySlot = new Map();
  for (const it of items) {
    if (!it || !it.id || !it.slot) continue;
    if (!bySlot.has(it.slot)) bySlot.set(it.slot, []);
    bySlot.get(it.slot).push(it);
  }
  const slots = Array.from(bySlot.keys());
  if (slots.length < 2) return [];
  const s1 = slots[Math.floor(Math.random() * slots.length)];
  const rest = slots.filter((s) => s !== s1);
  const s2 = rest[Math.floor(Math.random() * rest.length)];
  const list1 = bySlot.get(s1);
  const list2 = bySlot.get(s2);
  const a1 = list1[Math.floor(Math.random() * list1.length)];
  const a2 = list2[Math.floor(Math.random() * list2.length)];
  return [a1.id, a2.id];
}

let surpriseWorkerRunning = false;

function readSurpriseProgress() {
  try {
    if (fs.existsSync(SURPRISE_PROGRESS_PATH)) {
      const p = JSON.parse(fs.readFileSync(SURPRISE_PROGRESS_PATH, 'utf8'));
      return {
        status: p.status || 'idle',
        current: Number(p.current || 0),
        total: Number(p.total || 150),
        labelZh: p.labelZh || '',
        labelEn: p.labelEn || ''
      };
    }
  } catch {}
  if (fs.existsSync(SURPRISE_MANIFEST_PATH)) {
    return { status: 'done', current: 150, total: 150, labelZh: '', labelEn: '' };
  }
  return { status: surpriseWorkerRunning ? 'generating' : 'idle', current: 0, total: 150, labelZh: '', labelEn: '' };
}

function ensureSurpriseGenerationStarted() {
  if (fs.existsSync(SURPRISE_MANIFEST_PATH) || surpriseWorkerRunning) return;
  if (!fs.existsSync(SURPRISE_WORKER_PATH)) return;
  surpriseWorkerRunning = true;
  try {
    fs.mkdirSync(SURPRISE_DIR, { recursive: true });
    fs.writeFileSync(SURPRISE_PROGRESS_PATH, JSON.stringify({
      status: 'generating',
      current: 0,
      total: 150,
      labelZh: '正在启动本地 Agent 生成配饰库…',
      labelEn: 'Starting local Agent to generate accessories...',
      updatedAt: new Date().toISOString()
    }), 'utf8');
  } catch {}
  const child = spawn(process.execPath, [SURPRISE_WORKER_PATH], {
    cwd: PLUGIN_ROOT,
    stdio: 'ignore',
    windowsHide: true
  });
  child.on('exit', async () => {
    surpriseWorkerRunning = false;
    try {
      const cfg = readOfflineConfig();
      const patch = { SurprisePackGenerated: fs.existsSync(SURPRISE_MANIFEST_PATH) };
      if (!Array.isArray(cfg.SurpriseCurrentPair) || cfg.SurpriseCurrentPair.length < 2) {
        patch.SurpriseCurrentPair = pickTwoDistinctSlotAccessories();
        patch.SurpriseCountdown = 1 + Math.floor(Math.random() * 5);
      }
      writeOfflineConfig(patch);
      const health = await httpRequest('GET', '/health', null, 600);
      if (health.ok && health.data && health.data.running) {
        await httpRequest('POST', '/control', { action: 'reloadsurprise', value: 'true' });
      }
    } catch {}
  });
}

function attachSurpriseMeta(status, cfg) {
  const catalog = loadSurpriseCatalog();
  const byId = new Map(catalog.map((x) => [x.id, x]));
  const pairIds = Array.isArray(cfg.SurpriseCurrentPair) ? cfg.SurpriseCurrentPair : [];
  status.surprisePair = pairIds.map((id) => {
    const it = byId.get(id);
    return it ? { id: it.id, slot: it.slot, nameZh: it.nameZh, nameEn: it.nameEn } : { id, slot: '', nameZh: id, nameEn: id };
  });
  status.surpriseCountdown = Number(cfg.SurpriseCountdown || 0);
  status.surprisePackGenerated = fs.existsSync(SURPRISE_MANIFEST_PATH) || !!cfg.SurprisePackGenerated;
  status.surpriseTotalCount = catalog.length;
  status.surpriseProgress = readSurpriseProgress();
  return status;
}

function isAccessoryUnlocked(acc, done, starUnlocked = false) {
  const norm = String(acc || 'none').toLowerCase();
  if (norm === 'none' || !norm) return true;
  if (starUnlocked) return true;
  const req = ACCESSORY_MILESTONES[norm];
  return typeof req === 'number' && Number(done || 0) >= req;
}

function resolveEffectiveAccessory(pref, done, starUnlocked = false) {
  const norm = String(pref || 'none').toLowerCase();
  const c = Number(done || 0);
  if (norm === 'none' || norm === 'auto' || !norm) return 'none';
  return isAccessoryUnlocked(norm, c, starUnlocked) ? norm : 'none';
}

function buildOfflineStatus(cfg) {
  const effLang = detectEffectiveLang(cfg);
  const availablePets = scanPetPacks(effLang);
  const skinId = cfg.Skin || 'cyber-fox';
  const activePet = availablePets.find((p) => p.id === skinId);
  const accessorySupported = !(activePet && activePet.isCustom);
  return {
    ok: true,
    running: false,
    enabled: cfg.Enabled !== false,
    state: 'idle',
    skin: skinId,
    displayName: skinId,
    visible: false,
    scale: cfg.Scale || 1.0,
    bubbleMode: cfg.BubbleMode || 'always',
    bubbleStyle: cfg.BubbleStyle || 'simple',
    pinnedConversationId: cfg.PinnedConversationId || '',
    readyTimeoutSec: typeof cfg.ReadyTimeoutSec === 'number' ? cfg.ReadyTimeoutSec : 30,
    snapToAgy: !!cfg.SnapToAgy,
    hideOnFullscreen: cfg.HideOnFullscreen !== false,
    accessory: (cfg.Accessory && String(cfg.Accessory).toLowerCase() !== 'auto') ? cfg.Accessory : 'none',
    effectiveAccessory: accessorySupported ? resolveEffectiveAccessory(cfg.Accessory, cfg.CompletedTasksCount, !!cfg.AccessoriesStarUnlocked) : 'none',
    accessorySupported,
    accessoriesStarUnlocked: !!cfg.AccessoriesStarUnlocked,
    accessoryMilestones: ACCESSORY_MILESTONES,
    autoWalk: cfg.AutoWalk !== false,
    eyeFollow: cfg.EyeFollow !== false,
    boredomIdle: cfg.BoredomIdle !== false,
    keyMouseInteract: cfg.KeyMouseInteract !== false,
    topmost: cfg.Topmost !== false,
    autoStartOnBoot: cfg.AutoStartOnBoot !== false,
    checkUpdatesOnStartup: cfg.CheckUpdatesOnStartup !== false,
    soundAlert: !!cfg.SoundAlert,
    language: cfg.Language || 'auto',
    effectiveLanguage: effLang,
    activeTasks: 0,
    completedTasksCount: cfg.CompletedTasksCount || 0,
    toolCallsCount: cfg.ToolCallsCount || 0,
    uptimeMinutes: 0,
    activeConversationId: 'default',
    lastConversationId: 'default',
    recentConversations: [],
    availablePets: scanPetPacks(effLang)
  };
}

function ghApiGetJson(urlPath, timeoutMs = 4000) {
  return new Promise((resolve) => {
    const req = https.request(
      {
        hostname: 'api.github.com',
        path: urlPath,
        method: 'GET',
        headers: {
          'User-Agent': 'antigravity-pet',
          'Accept': 'application/vnd.github+json',
          'Cache-Control': 'no-cache'
        }
      },
      (res) => {
        let buf = '';
        res.on('data', (c) => { buf += c; });
        res.on('end', () => {
          if (res.statusCode >= 200 && res.statusCode < 300) {
            try { resolve(JSON.parse(buf)); return; } catch {}
          }
          resolve(null);
        });
      }
    );
    req.setTimeout(timeoutMs, () => { req.destroy(); resolve(null); });
    req.on('error', () => resolve(null));
    req.end();
  });
}

function checkGhCliStarred() {
  return new Promise((resolve) => {
    execFile('gh', ['api', 'user/starred/Wlaeg/antigravity-pet'], { timeout: 2500 }, (err) => {
      resolve(!err);
    });
  });
}

async function fetchRepoStarSnapshot() {
  const repo = await ghApiGetJson('/repos/Wlaeg/antigravity-pet');
  if (!repo || typeof repo.stargazers_count !== 'number') return null;
  const count = repo.stargazers_count;
  let tailLogins = [];
  if (count > 0) {
    const lastPage = Math.max(1, Math.ceil(count / 100));
    const list = await ghApiGetJson(`/repos/Wlaeg/antigravity-pet/stargazers?per_page=100&page=${lastPage}`);
    if (Array.isArray(list)) {
      tailLogins = list.map((u) => String(u && u.login ? u.login : '').toLowerCase()).filter(Boolean);
    }
  }
  return { count, tailLogins };
}

const starVerifySession = {
  state: 'idle', // idle | verifying | timeout
  deadline: 0,
  baseCount: -1,
  baseTail: new Set(),
  timer: null,
  checking: false
};

function stopStarVerifyTimer() {
  if (starVerifySession.timer) {
    clearInterval(starVerifySession.timer);
    starVerifySession.timer = null;
  }
}

async function completeStarUnlock() {
  stopStarVerifyTimer();
  starVerifySession.state = 'idle';
  starVerifySession.deadline = 0;
  writeOfflineConfig({ AccessoriesStarUnlocked: true });
  const health = await httpRequest('GET', '/health', null, 600);
  if (health.ok && health.data && health.data.running) {
    await httpRequest('POST', '/control', { action: 'starunlock', value: 'true' });
  }
}

async function runStarVerifyCheck() {
  if (starVerifySession.checking) return false;
  starVerifySession.checking = true;
  try {
    const cfg = readOfflineConfig();
    if (cfg.AccessoriesStarUnlocked) {
      await completeStarUnlock();
      return true;
    }
    if (await checkGhCliStarred()) {
      await completeStarUnlock();
      return true;
    }
    const snap = await fetchRepoStarSnapshot();
    if (snap && starVerifySession.baseCount >= 0) {
      const hasNewCount = snap.count > starVerifySession.baseCount;
      const hasNewLogin = snap.tailLogins.some((u) => !starVerifySession.baseTail.has(u));
      if (hasNewCount || hasNewLogin) {
        await completeStarUnlock();
        return true;
      }
    }
    if (starVerifySession.state === 'verifying' && Date.now() >= starVerifySession.deadline) {
      stopStarVerifyTimer();
      starVerifySession.state = 'timeout';
    }
    return false;
  } finally {
    starVerifySession.checking = false;
  }
}

async function startStarVerify() {
  stopStarVerifyTimer();
  const cfg = readOfflineConfig();
  if (cfg.AccessoriesStarUnlocked) {
    starVerifySession.state = 'idle';
    return;
  }
  if (await checkGhCliStarred()) {
    await completeStarUnlock();
    return;
  }
  const snap = await fetchRepoStarSnapshot();
  starVerifySession.baseCount = snap ? snap.count : -1;
  starVerifySession.baseTail = new Set(snap ? snap.tailLogins : []);
  starVerifySession.state = 'verifying';
  starVerifySession.deadline = Date.now() + 90_000;
  starVerifySession.timer = setInterval(() => {
    runStarVerifyCheck().catch(() => {});
  }, 4000);
}

function attachStarVerifyMeta(status) {
  const unlocked = !!status.accessoriesStarUnlocked;
  const isVerifying = !unlocked && starVerifySession.state === 'verifying' && Date.now() < starVerifySession.deadline;
  if (!unlocked && starVerifySession.state === 'verifying' && Date.now() >= starVerifySession.deadline) {
    stopStarVerifyTimer();
    starVerifySession.state = 'timeout';
  }
  status.starVerifyState = unlocked ? 'unlocked' : starVerifySession.state;
  status.starVerifyRemainingSec = isVerifying ? Math.max(0, Math.ceil((starVerifySession.deadline - Date.now()) / 1000)) : 0;
  return status;
}

export async function getSnapshot() {
  const res = await httpRequest('GET', '/health', null, 600);
  if (res.ok && res.data) {
    const effLang = res.data.effectiveLanguage || 'zh';
    res.data.availablePets = scanPetPacks(effLang);
    if (typeof res.data.accessorySupported !== 'boolean') {
      const activePet = res.data.availablePets.find((p) => p.id === res.data.skin);
      res.data.accessorySupported = !(activePet && activePet.isCustom);
    }
    res.data.accessoryMilestones = ACCESSORY_MILESTONES;
    return attachSurpriseMeta(attachStarVerifyMeta(res.data), readOfflineConfig());
  }
  const cfg = readOfflineConfig();
  return attachSurpriseMeta(attachStarVerifyMeta(buildOfflineStatus(cfg)), cfg);
}

function parseBool(v, fallback = true) {
  if (typeof v === 'boolean') return v;
  const s = String(v || '').toLowerCase();
  if (s === 'true' || s === '1') return true;
  if (s === 'false' || s === '0') return false;
  return fallback;
}

export async function applySetting(args = {}) {
  const action = String(args.action || 'status');
  const value = args.value;

  const health = await httpRequest('GET', '/health', null, 500);
  const isRunning = health.ok && !!health.data;

  if (action === 'start' || action === 'show') {
    writeOfflineConfig({ Enabled: true });
    if (!isRunning) {
      await launchPetOnRealDesktop();
      await waitForHealth(3000);
    } else {
      await httpRequest('POST', '/control', { action: 'show' });
    }
  } else if (action === 'hide') {
    if (isRunning) await httpRequest('POST', '/control', { action: 'hide' });
  } else if (action === 'toggle') {
    if (!isRunning) {
      writeOfflineConfig({ Enabled: true });
      await launchPetOnRealDesktop();
      await waitForHealth(3000);
    } else {
      await httpRequest('POST', '/control', { action: 'toggle' });
    }
  } else if (action === 'quit') {
    writeOfflineConfig({ Enabled: false });
    if (isRunning) {
      await httpRequest('POST', '/control', { action: 'quit' });
      await new Promise((r) => setTimeout(r, 350));
    }
  } else if (action === 'skin') {
    const sk = String(value || args.skin || 'cyber-fox');
    writeOfflineConfig({ Skin: sk });
    if (isRunning) await httpRequest('POST', '/skin', { skin: sk });
  } else if (action === 'scale') {
    const sc = Number(value || args.scale || 1.0);
    writeOfflineConfig({ Scale: sc });
    if (isRunning) await httpRequest('POST', '/control', { action: 'scale', value: String(sc) });
  } else if (action === 'bubbleMode') {
    const bm = String(value || args.bubbleMode || 'always');
    writeOfflineConfig({ BubbleMode: bm });
    if (isRunning) await httpRequest('POST', '/control', { action: 'bubbleMode', value: bm });
  } else if (action === 'bubbleStyle') {
    const bs = String(value || args.bubbleStyle || 'simple').toLowerCase();
    writeOfflineConfig({ BubbleStyle: bs });
    if (isRunning) await httpRequest('POST', '/control', { action: 'bubbleStyle', value: bs });
  } else if (action === 'pinConversation') {
    const cid = String(value ?? '');
    writeOfflineConfig({ PinnedConversationId: cid });
    if (isRunning) await httpRequest('POST', '/control', { action: 'pinConversation', value: cid });
  } else if (action === 'readyTimeoutSec') {
    const sec = Math.max(0, Math.min(3600, Number.isFinite(Number(value)) ? Math.round(Number(value)) : 30));
    writeOfflineConfig({ ReadyTimeoutSec: sec });
    if (isRunning) await httpRequest('POST', '/control', { action: 'readyTimeoutSec', value: String(sec) });
  } else if (action === 'autoWalk') {
    const b = parseBool(value, true);
    writeOfflineConfig({ AutoWalk: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'autoWalk', value: String(b) });
  } else if (action === 'eyeFollow') {
    const b = parseBool(value, true);
    writeOfflineConfig({ EyeFollow: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'eyeFollow', value: String(b) });
  } else if (action === 'boredomIdle') {
    const b = parseBool(value, true);
    writeOfflineConfig({ BoredomIdle: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'boredomIdle', value: String(b) });
  } else if (action === 'keyMouseInteract') {
    const b = parseBool(value, true);
    writeOfflineConfig({ KeyMouseInteract: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'keyMouseInteract', value: String(b) });
  } else if (action === 'topmost') {
    const b = parseBool(value, true);
    writeOfflineConfig({ Topmost: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'topmost', value: String(b) });
  } else if (action === 'autoStartOnBoot') {
    const b = parseBool(value, true);
    writeOfflineConfig({ AutoStartOnBoot: b });
    await syncRegistryAutoStart(b);
    if (isRunning) await httpRequest('POST', '/control', { action: 'autoStartOnBoot', value: String(b) });
  } else if (action === 'checkUpdatesOnStartup') {
    const b = parseBool(value, true);
    writeOfflineConfig({ CheckUpdatesOnStartup: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'checkUpdatesOnStartup', value: String(b) });
  } else if (action === 'soundAlert') {
    const b = parseBool(value, false);
    writeOfflineConfig({ SoundAlert: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'soundAlert', value: String(b) });
  } else if (action === 'language') {
    const lg = String(value || args.language || 'auto').toLowerCase();
    writeOfflineConfig({ Language: lg });
    if (isRunning) await httpRequest('POST', '/control', { action: 'language', value: lg });
  } else if (action === 'agyLocale') {
    const loc = String(value || '');
    writeOfflineConfig({ AgyDetectedLocale: loc });
    if (isRunning) await httpRequest('POST', '/control', { action: 'agyLocale', value: loc });
  } else if (action === 'snapToAgy') {
    const b = parseBool(value, false);
    writeOfflineConfig({ SnapToAgy: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'snapToAgy', value: String(b) });
  } else if (action === 'hideOnFullscreen') {
    const b = parseBool(value, true);
    writeOfflineConfig({ HideOnFullscreen: b });
    if (isRunning) await httpRequest('POST', '/control', { action: 'hideOnFullscreen', value: String(b) });
  } else if (action === 'accessory') {
    let acc = String(value || args.accessory || 'none').toLowerCase();
    if (acc === 'auto' || !acc) acc = 'none';
    const cfg = readOfflineConfig();
    const effLang = detectEffectiveLang(cfg);
    const pets = scanPetPacks(effLang);
    const activePet = pets.find((p) => p.id === (cfg.Skin || 'cyber-fox'));
    if (activePet && activePet.isCustom) {
      return {
        ok: false,
        unsupported: true,
        accessory: acc,
        status: isRunning ? (await httpRequest('GET', '/status', null, 600)).data || buildOfflineStatus(cfg) : buildOfflineStatus(cfg)
      };
    }
    const done = Number(cfg.CompletedTasksCount || 0);
    const starUnlocked = !!cfg.AccessoriesStarUnlocked;
    if (!isAccessoryUnlocked(acc, done, starUnlocked)) {
      return {
        ok: false,
        locked: true,
        accessory: acc,
        required: ACCESSORY_MILESTONES[acc] || 0,
        completedTasksCount: done,
        status: isRunning ? (await httpRequest('GET', '/status', null, 600)).data || buildOfflineStatus(cfg) : buildOfflineStatus(cfg)
      };
    }
    const patch = { Accessory: acc };
    if (acc === 'surprise') {
      if (!Array.isArray(cfg.SurpriseCurrentPair) || cfg.SurpriseCurrentPair.length < 2) {
        patch.SurpriseCurrentPair = pickTwoDistinctSlotAccessories();
        patch.SurpriseCountdown = 1 + Math.floor(Math.random() * 5);
      }
      ensureSurpriseGenerationStarted();
    }
    writeOfflineConfig(patch);
    if (isRunning) {
      if (patch.SurpriseCurrentPair) {
        await httpRequest('POST', '/control', { action: 'rerollsurprise', value: patch.SurpriseCurrentPair.join(',') });
      }
      await httpRequest('POST', '/control', { action: 'accessory', value: acc });
    }
  } else if (action === 'rerollSurprise') {
    const pair = pickTwoDistinctSlotAccessories();
    const countdown = 1 + Math.floor(Math.random() * 5);
    writeOfflineConfig({ SurpriseCurrentPair: pair, SurpriseCountdown: countdown });
    if (isRunning) await httpRequest('POST', '/control', { action: 'rerollsurprise', value: pair.join(',') });
  } else if (action === 'startStarVerify') {
    await startStarVerify();
  } else if (action === 'checkStarVerify') {
    await runStarVerifyCheck();
  } else if (action === 'starUnlock') {
    await completeStarUnlock();
  } else if (action === 'quickDispatch') {
    if (isRunning) await httpRequest('POST', '/control', { action: 'quickDispatch' });
  } else if (action === 'resetPos') {
    writeOfflineConfig({ Left: -1, Top: -1 });
    if (isRunning) await httpRequest('POST', '/control', { action: 'resetPos' });
  } else if (action === 'reload') {
    if (isRunning) await httpRequest('POST', '/reload', {});
  } else if (action === 'wave' || action === 'jump' || action === 'walk' || action === 'dockLeft' || action === 'dockRight' || action === 'undock') {
    if (isRunning) await httpRequest('POST', '/control', { action });
  } else if (action === 'openConversation') {
    if (isRunning) await httpRequest('POST', '/control', { action: 'open', value: String(value || '') });
  } else if (action === 'testState') {
    if (!isRunning) {
      await launchPetOnRealDesktop();
      await waitForHealth(3000);
    }
    await httpRequest('POST', '/state', {
      conversationId: 'preview',
      state: String(args.state || 'idle'),
      message: String(args.message || ''),
      isBackground: !!args.isBackground
    });
  } else if (action === 'testUnlock') {
    if (!isRunning) {
      await launchPetOnRealDesktop();
      await waitForHealth(3000);
    }
    await httpRequest('POST', '/control', {
      action: 'testunlock',
      value: String(value || args.accessory || '')
    });
  } else if (action === 'testAccessory') {
    if (!isRunning) {
      await launchPetOnRealDesktop();
      await waitForHealth(3000);
    }
    await httpRequest('POST', '/control', {
      action: 'testaccessory',
      value: String(value || args.accessory || '')
    });
  }

  return await getSnapshot();
}

function findPetManifestDir(rootDir) {
  if (fs.existsSync(path.join(rootDir, 'pet.json'))) return rootDir;
  for (const entry of fs.readdirSync(rootDir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      const sub = path.join(rootDir, entry.name);
      if (fs.existsSync(path.join(sub, 'pet.json'))) return sub;
    }
  }
  return null;
}

function copyDirRecursive(src, dst) {
  fs.mkdirSync(dst, { recursive: true });
  for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
    const s = path.join(src, entry.name);
    const d = path.join(dst, entry.name);
    if (entry.isDirectory()) copyDirRecursive(s, d);
    else fs.copyFileSync(s, d);
  }
}

function downloadHttpsBuffer(targetUrl, maxBytes = 20 * 1024 * 1024, timeoutMs = 10000, redirectsLeft = 3) {
  return new Promise((resolve, reject) => {
    let parsed;
    try { parsed = new URL(String(targetUrl || '')); } catch { return reject(new Error('Invalid URL')); }
    if (parsed.protocol !== 'https:') return reject(new Error('Only HTTPS URLs are allowed'));
    const req = https.get(parsed, { headers: { 'User-Agent': 'AntigravityPet-Panel' }, timeout: timeoutMs }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location && redirectsLeft > 0) {
        res.resume();
        const nextUrl = new URL(res.headers.location, parsed).toString();
        downloadHttpsBuffer(nextUrl, maxBytes, timeoutMs, redirectsLeft - 1).then(resolve, reject);
        return;
      }
      if (res.statusCode < 200 || res.statusCode >= 300) {
        res.resume();
        return reject(new Error(`HTTP ${res.statusCode}`));
      }
      const chunks = [];
      let total = 0;
      res.on('data', (chunk) => {
        total += chunk.length;
        if (total > maxBytes) {
          req.destroy(new Error('File exceeds maximum allowed size'));
          return;
        }
        chunks.push(chunk);
      });
      res.on('end', () => resolve(Buffer.concat(chunks)));
      res.on('error', reject);
    });
    req.on('timeout', () => req.destroy(new Error('Request timeout')));
    req.on('error', reject);
  });
}

async function extractZipToDir(zipPath, extractDir) {
  fs.mkdirSync(extractDir, { recursive: true });
  await new Promise((resolve, reject) => {
    if (IS_WIN) {
      execFile('powershell.exe', ['-NoProfile', '-Command', `Expand-Archive -LiteralPath '${zipPath.replace(/'/g, "''")}' -DestinationPath '${extractDir.replace(/'/g, "''")}' -Force`], { timeout: 15000, windowsHide: true }, (err) => (err ? reject(err) : resolve()));
    } else {
      execFile('unzip', ['-o', zipPath, '-d', extractDir], { timeout: 15000 }, (err) => (err ? reject(err) : resolve()));
    }
  });
}

const _avatarMemCache = new Map();

function readImageAsDataUrl(filePath) {
  if (!filePath || !fs.existsSync(filePath)) return null;
  const ext = path.extname(filePath).toLowerCase();
  const mime = ext === '.webp' ? 'image/webp' : (ext === '.jpg' || ext === '.jpeg' ? 'image/jpeg' : 'image/png');
  const buf = fs.readFileSync(filePath);
  if (!buf || buf.length === 0) return null;
  return `data:${mime};base64,${buf.toString('base64')}`;
}

function resolvePetPreviewDataUrl(petDir) {
  try {
    const manifestPath = path.join(petDir, 'pet.json');
    if (!fs.existsSync(manifestPath)) return null;
    const meta = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    const sheetPath = path.join(petDir, meta.spritesheetPath || 'spritesheet.png');
    if (fs.existsSync(sheetPath)) return readImageAsDataUrl(sheetPath);
    const idleDir = path.join(petDir, 'idle');
    if (fs.existsSync(idleDir)) {
      const frames = fs.readdirSync(idleDir).filter((f) => /\.(png|webp|jpg|jpeg)$/i.test(f)).sort();
      if (frames.length) return readImageAsDataUrl(path.join(idleDir, frames[0]));
    }
    const candidates = [meta.idleImage, meta.image, 'idle.png', 'idle.webp', 'pet.png', 'pet.webp', 'avatar.png', 'avatar.webp'].filter(Boolean);
    for (const rel of candidates) {
      const fp = path.join(petDir, String(rel));
      if (fs.existsSync(fp)) return readImageAsDataUrl(fp);
    }
    const rootImg = fs.readdirSync(petDir).find((f) => /\.(png|webp|jpg|jpeg)$/i.test(f));
    if (rootImg) return readImageAsDataUrl(path.join(petDir, rootImg));
  } catch {}
  return null;
}

export async function getPetAvatars() {
  const avatars = {};
  for (const root of [PETS_DIR, USER_PETS_DIR]) {
    try {
      if (!fs.existsSync(root)) continue;
      for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
        if (!entry.isDirectory()) continue;
        const dir = path.join(root, entry.name);
        const manifest = path.join(dir, 'pet.json');
        if (!fs.existsSync(manifest)) continue;
        try {
          const meta = JSON.parse(fs.readFileSync(manifest, 'utf8'));
          const id = String(meta.id || entry.name);
          const dataUrl = resolvePetPreviewDataUrl(dir);
          if (dataUrl) {
            avatars[id] = dataUrl;
            _avatarMemCache.set(id.toLowerCase(), dataUrl);
          }
        } catch {}
      }
    } catch {}
  }

  // Fill community pack previews from local fallback zips (without re-extracting if cached)
  if (fs.existsSync(COMMUNITY_MANIFEST_PATH)) {
    try {
      const manifest = JSON.parse(fs.readFileSync(COMMUNITY_MANIFEST_PATH, 'utf8'));
      for (const p of (manifest.pets || [])) {
        const id = String(p.id || '').trim();
        const key = id.toLowerCase();
        if (!id || avatars[id]) continue;
        if (_avatarMemCache.has(key)) {
          avatars[id] = _avatarMemCache.get(key);
          continue;
        }
        const prevRel = String(p.localPreview || `community/previews/${id}.png`).replace(/^[\\/]+/, '');
        const prevResolved = path.resolve(PLUGIN_ROOT, prevRel);
        const communityRoot = path.resolve(PLUGIN_ROOT, 'community');
        if (prevResolved.startsWith(communityRoot) && fs.existsSync(prevResolved)) {
          const prevDataUrl = readImageAsDataUrl(prevResolved);
          if (prevDataUrl) {
            avatars[id] = prevDataUrl;
            _avatarMemCache.set(key, prevDataUrl);
            continue;
          }
        }
        const rel = String(p.localZip || '').replace(/^[\\/]+/, '');
        const resolved = rel ? path.resolve(PLUGIN_ROOT, rel) : '';
        if (resolved && resolved.startsWith(communityRoot) && fs.existsSync(resolved)) {
          const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'agy-avatar-'));
          try {
            await extractZipToDir(resolved, tmpDir);
            const mDir = findPetManifestDir(tmpDir);
            const dataUrl = mDir ? resolvePetPreviewDataUrl(mDir) : null;
            if (dataUrl) {
              avatars[id] = dataUrl;
              _avatarMemCache.set(key, dataUrl);
            }
          } catch {} finally {
            try { fs.rmSync(tmpDir, { recursive: true, force: true }); } catch {}
          }
        }
      }
    } catch {}
  }
  return { ok: true, avatars };
}

function readCommunityStars() {
  try {
    if (!fs.existsSync(COMMUNITY_STARS_PATH)) return new Set();
    const arr = JSON.parse(fs.readFileSync(COMMUNITY_STARS_PATH, 'utf8'));
    return new Set(Array.isArray(arr) ? arr.map((s) => String(s).toLowerCase()) : []);
  } catch {
    return new Set();
  }
}

export async function toggleCommunityStar(args = {}) {
  const id = String(args.id || '').trim().toLowerCase();
  if (!id) return { ok: false, error: 'Missing pet id' };
  const set = readCommunityStars();
  let starred;
  if (set.has(id)) {
    set.delete(id);
    starred = false;
  } else {
    set.add(id);
    starred = true;
  }
  try {
    fs.mkdirSync(path.dirname(COMMUNITY_STARS_PATH), { recursive: true });
    fs.writeFileSync(COMMUNITY_STARS_PATH, JSON.stringify(Array.from(set), null, 2), 'utf8');
  } catch (err) {
    return { ok: false, error: err && err.message ? err.message : String(err) };
  }
  return { ok: true, id, starred };
}

function readCommunityDownloads() {
  try {
    if (!fs.existsSync(COMMUNITY_DOWNLOADS_PATH)) return {};
    const obj = JSON.parse(fs.readFileSync(COMMUNITY_DOWNLOADS_PATH, 'utf8'));
    return obj && typeof obj === 'object' ? obj : {};
  } catch {
    return {};
  }
}

function incrementCommunityDownload(id) {
  const key = String(id || '').trim().toLowerCase();
  if (!key) return 0;
  const map = readCommunityDownloads();
  map[key] = Math.max(0, Number(map[key] || 0)) + 1;
  try {
    fs.mkdirSync(path.dirname(COMMUNITY_DOWNLOADS_PATH), { recursive: true });
    fs.writeFileSync(COMMUNITY_DOWNLOADS_PATH, JSON.stringify(map, null, 2), 'utf8');
  } catch {}
  return map[key];
}

async function fetchAbacusCount(id, mode = 'get', timeoutMs = 2000) {
  const key = String(id || '').trim().toLowerCase().replace(/[^a-z0-9-_]/g, '-');
  if (!key) return null;
  try {
    const buf = await downloadHttpsBuffer(`${ABACUS_BASE_URL}/${mode}/${ABACUS_NS}/dl-${key}`, 64 * 1024, timeoutMs, 2);
    const data = JSON.parse(buf.toString('utf8'));
    if (data && typeof data.value === 'number' && Number.isFinite(data.value)) {
      return Math.max(0, data.value);
    }
  } catch (err) {
    if (mode === 'get' && err && /HTTP 404/.test(String(err.message || ''))) {
      return 0;
    }
  }
  return null;
}

export async function getCommunityPets() {
  let manifest = null;
  let globalStats = {};
  let source = 'local';
  const [manifestRes, statsRes] = await Promise.allSettled([
    downloadHttpsBuffer(COMMUNITY_MANIFEST_URL + '?t=' + Date.now(), 1024 * 1024, 3500),
    downloadHttpsBuffer(COMMUNITY_STATS_URL + '?t=' + Date.now(), 512 * 1024, 3000)
  ]);
  if (manifestRes.status === 'fulfilled') {
    try {
      const parsed = JSON.parse(manifestRes.value.toString('utf8'));
      if (parsed && Array.isArray(parsed.pets)) {
        manifest = parsed;
        source = 'remote';
      }
    } catch {}
  }
  if (statsRes.status === 'fulfilled') {
    try {
      const parsedStats = JSON.parse(statsRes.value.toString('utf8'));
      if (parsedStats && parsedStats.pets && typeof parsedStats.pets === 'object') {
        globalStats = parsedStats.pets;
      }
    } catch {}
  }
  if (!manifest && fs.existsSync(COMMUNITY_MANIFEST_PATH)) {
    try {
      manifest = JSON.parse(fs.readFileSync(COMMUNITY_MANIFEST_PATH, 'utf8'));
    } catch {}
  }
  if (!Object.keys(globalStats).length && fs.existsSync(COMMUNITY_STATS_PATH)) {
    try {
      const parsedStats = JSON.parse(fs.readFileSync(COMMUNITY_STATS_PATH, 'utf8'));
      if (parsedStats && parsedStats.pets) globalStats = parsedStats.pets;
    } catch {}
  }
  const rawPets = manifest && Array.isArray(manifest.pets) ? manifest.pets : [];
  const liveEntries = await Promise.all(
    rawPets.map(async (p) => {
      const key = String(p.id || '').toLowerCase();
      return [key, await fetchAbacusCount(key, 'get', 2000)];
    })
  );
  const liveDownloads = Object.fromEntries(liveEntries);
  const installedIds = new Set(scanPetPacks().map((p) => String(p.id || '').toLowerCase()));
  const starredSet = readCommunityStars();
  const downloadsMap = readCommunityDownloads();
  const pets = rawPets.map((p) => {
    const key = String(p.id || '').toLowerCase();
    const starred = starredSet.has(key);
    const stat = globalStats[key] || {};
    const baseStars = Math.max(Number(p.stars || 0), Number(stat.stars || 0));
    const liveVal = liveDownloads[key];
    const hasLive = typeof liveVal === 'number';
    const baseDownloads = Math.max(
      Number(p.downloads || 0),
      Number(stat.downloads || 0),
      hasLive ? liveVal : 0
    );
    const localDownloads = hasLive ? 0 : Number(downloadsMap[key] || 0);
    const githubUrl = p.githubUrl || (p.localZip
      ? `https://github.com/Wlaeg/antigravity-pet/blob/main/${String(p.localZip).replace(/^[\\/]+/, '')}`
      : 'https://github.com/Wlaeg/antigravity-pet');
    return {
      ...p,
      starred,
      stars: baseStars + (starred ? 1 : 0),
      downloads: baseDownloads + localDownloads,
      githubUrl,
      installed: installedIds.has(key)
    };
  });
  return { ok: true, source, pets };
}

export async function importPetPack(args = {}) {
  const tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'agy-pet-import-'));
  try {
    let sourceDir = null;
    if (args.petJson && (args.idleImageBase64 || args.spritesheetBase64)) {
      const metaObj = typeof args.petJson === 'string' ? JSON.parse(args.petJson) : args.petJson;
      const rawB64 = String(args.idleImageBase64 || args.spritesheetBase64).replace(/^data:image\/[a-zA-Z0-9+.-]+;base64,/, '');
      const imgBuf = Buffer.from(rawB64, 'base64');
      if (imgBuf.length === 0 || imgBuf.length > 12 * 1024 * 1024) {
        return { ok: false, error: '无效的图片数据' };
      }
      sourceDir = path.join(tmpRoot, 'studio');
      fs.mkdirSync(sourceDir, { recursive: true });
      fs.writeFileSync(path.join(sourceDir, 'pet.json'), JSON.stringify(metaObj, null, 2), 'utf8');
      const targetImg = args.idleImageBase64
        ? (metaObj.idleImage || 'idle.png')
        : (metaObj.spritesheetPath || 'spritesheet.png');
      fs.writeFileSync(path.join(sourceDir, targetImg), imgBuf);
    } else if (args.zipBase64) {
      const cleanB64 = String(args.zipBase64).replace(/^data:[^;]+;base64,/, '');
      const zipPath = path.join(tmpRoot, 'pack.zip');
      const extractDir = path.join(tmpRoot, 'extracted');
      fs.writeFileSync(zipPath, Buffer.from(cleanB64, 'base64'));
      await extractZipToDir(zipPath, extractDir);
      sourceDir = findPetManifestDir(extractDir);
    } else if (args.zipUrl || args.rawZipUrl || args.localZip) {
      const zipPath = path.join(tmpRoot, 'community.zip');
      let downloaded = false;
      for (const candidateUrl of [args.zipUrl, args.rawZipUrl].filter(Boolean)) {
        try {
          const buf = await downloadHttpsBuffer(candidateUrl);
          fs.writeFileSync(zipPath, buf);
          downloaded = true;
          break;
        } catch {}
      }
      if (!downloaded && args.localZip) {
        const rel = String(args.localZip).replace(/^[\\/]+/, '');
        const resolved = path.resolve(PLUGIN_ROOT, rel);
        const communityRoot = path.resolve(PLUGIN_ROOT, 'community');
        if (resolved.startsWith(communityRoot) && fs.existsSync(resolved)) {
          fs.copyFileSync(resolved, zipPath);
          downloaded = true;
        }
      }
      if (!downloaded) {
        return { ok: false, error: '拉取社区宠物包失败，请检查网络连接' };
      }
      const extractDir = path.join(tmpRoot, 'extracted');
      await extractZipToDir(zipPath, extractDir);
      sourceDir = findPetManifestDir(extractDir);
    } else if (args.localPath) {
      const lp = path.resolve(String(args.localPath));
      if (fs.existsSync(lp) && fs.statSync(lp).isDirectory()) {
        sourceDir = findPetManifestDir(lp);
      } else if (fs.existsSync(lp) && lp.toLowerCase().endsWith('.zip')) {
        const extractDir = path.join(tmpRoot, 'extracted');
        await extractZipToDir(lp, extractDir);
        sourceDir = findPetManifestDir(extractDir);
      }
    }

    if (!sourceDir) {
      return { ok: false, error: '未在宠物包中找到 pet.json 清单文件' };
    }

    const meta = JSON.parse(fs.readFileSync(path.join(sourceDir, 'pet.json'), 'utf8'));
    const rawId = String(meta.id || path.basename(sourceDir) || 'custom-pet').toLowerCase();
    const safeId = rawId.replace(/[^a-z0-9-_]/g, '-').replace(/^-+|-+$/g, '') || 'custom-pet';
    const hasSheet = fs.existsSync(path.join(sourceDir, meta.spritesheetPath || 'spritesheet.png'));
    const hasIdleFrames = fs.existsSync(path.join(sourceDir, 'idle'));
    const singleCandidates = [meta.idleImage, meta.image, 'idle.png', 'idle.webp', 'pet.png', 'pet.webp', 'avatar.png', 'avatar.webp'].filter(Boolean);
    const hasSingleDeclared = singleCandidates.some((rel) => fs.existsSync(path.join(sourceDir, String(rel))));
    const hasRootImage = fs.readdirSync(sourceDir).some((f) => /\.(png|webp)$/i.test(f));
    if (!hasSheet && !hasIdleFrames && !hasSingleDeclared && !hasRootImage) {
      return { ok: false, error: '宠物包缺少 spritesheet.png、idle/ 序列帧目录或基础单图（如 idle.png）' };
    }

    const destDir = path.join(USER_PETS_DIR, safeId);
    copyDirRecursive(sourceDir, destDir);

    const health = await httpRequest('GET', '/health', null, 500);
    if (health.ok) {
      await httpRequest('POST', '/reload', {});
      await httpRequest('POST', '/skin', { skin: safeId });
    } else {
      writeOfflineConfig({ Skin: safeId });
    }
    const snapshot = await getSnapshot();
    return { ok: true, id: safeId, displayName: meta.displayName || safeId, snapshot };
  } catch (err) {
    return { ok: false, error: err && err.message ? err.message : String(err) };
  } finally {
    try { fs.rmSync(tmpRoot, { recursive: true, force: true }); } catch {}
  }
}

export async function installCommunityPet(args = {}) {
  const id = String(args.id || '').trim().toLowerCase();
  let target = null;
  if (id) {
    const listRes = await getCommunityPets();
    target = (listRes.pets || []).find((p) => String(p.id || '').toLowerCase() === id) || null;
  }
  const zipUrl = (target && target.zipUrl) || args.zipUrl || '';
  const rawZipUrl = (target && target.rawZipUrl) || args.rawZipUrl || '';
  const localZip = (target && target.localZip) || args.localZip || '';
  if (!zipUrl && !rawZipUrl && !localZip) {
    return { ok: false, error: '未找到对应的社区宠物包下载地址' };
  }
  const res = await importPetPack({ zipUrl, rawZipUrl, localZip });
  if (res && res.ok) {
    const petKey = id || res.id;
    const remoteDl = await fetchAbacusCount(petKey, 'hit', 3000);
    if (typeof remoteDl === 'number') {
      res.downloads = remoteDl;
    } else {
      incrementCommunityDownload(petKey);
    }
  }
  return res;
}

export async function deleteCustomPet(args = {}) {
  const rawId = String(args.id || '').trim().toLowerCase();
  const safeId = rawId.replace(/[^a-z0-9-_]/g, '-').replace(/^-+|-+$/g, '');
  if (!safeId) return { ok: false, error: 'Missing pet id' };
  const targetDir = path.resolve(USER_PETS_DIR, safeId);
  const userRoot = path.resolve(USER_PETS_DIR);
  if (!targetDir.startsWith(userRoot + path.sep) || !fs.existsSync(targetDir)) {
    return { ok: false, error: 'Custom pet not found' };
  }
  try {
    fs.rmSync(targetDir, { recursive: true, force: true });
    _avatarMemCache.delete(safeId);
    const snap = await getSnapshot();
    const health = await httpRequest('GET', '/health', null, 500);
    if (health.ok) {
      if (String(snap.skin || '').toLowerCase() === safeId) {
        await httpRequest('POST', '/skin', { skin: 'cyber-fox' });
      }
      await httpRequest('POST', '/reload', {});
    } else if (String(snap.skin || '').toLowerCase() === safeId) {
      writeOfflineConfig({ Skin: 'cyber-fox' });
    }
    const nextSnapshot = await getSnapshot();
    return { ok: true, id: safeId, snapshot: nextSnapshot };
  } catch (err) {
    return { ok: false, error: err && err.message ? err.message : String(err) };
  }
}

function startLocalDispatchBridge(app) {
  const token = crypto.randomBytes(16).toString('hex');
  const bridgeFile = path.join(os.homedir(), '.antigravity-pet', 'panel-bridge.json');
  const srv = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://127.0.0.1');
    if (req.method === 'POST' && url.pathname === '/api/dispatch') {
      let raw = '';
      req.on('data', (c) => (raw += c));
      req.on('end', async () => {
        try {
          const data = raw ? JSON.parse(raw) : {};
          if (data.token !== token) {
            res.writeHead(403, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ ok: false, error: 'forbidden' }));
            return;
          }
          const msg = String(data.message || '').trim();
          if (!msg) {
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ ok: false, error: 'empty_message' }));
            return;
          }
          let cid = String(data.conversationId || '').trim();
          const forceNew = data.newConversation === true || !cid || cid === 'default' || cid === 'preview';
          if (forceNew && typeof app.startConversation === 'function') {
            const created = await app.startConversation(msg);
            cid = (created && (created.conversationId || created.id)) || cid;
          } else {
            if (forceNew && typeof app.newConversation === 'function') {
              const created = await app.newConversation();
              cid = (created && (created.conversationId || created.id)) || cid;
            }
            if (typeof app.sendMessage === 'function') {
              await app.sendMessage(cid, msg);
            }
          }
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ ok: true, conversationId: cid }));
        } catch (err) {
          res.writeHead(500, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ ok: false, error: err?.message || String(err) }));
        }
      });
      return;
    }
    res.writeHead(404);
    res.end('Not Found');
  });
  srv.listen(0, '127.0.0.1', () => {
    try {
      const addr = srv.address();
      fs.mkdirSync(path.dirname(bridgeFile), { recursive: true });
      fs.writeFileSync(bridgeFile, JSON.stringify({ port: addr.port, token }), 'utf8');
    } catch {}
  });
}

let _releasesCache = { ts: 0, data: null };

export async function getGithubReleases(forceRefresh = false) {
  let currentVersion = '0.0.0';
  try {
    const manifest = JSON.parse(fs.readFileSync(path.join(PLUGIN_ROOT, 'plugin.json'), 'utf8'));
    if (manifest && manifest.version) currentVersion = String(manifest.version);
  } catch {}
  const now = Date.now();
  if (!forceRefresh && _releasesCache.data && (now - _releasesCache.ts) < 60000) {
    return { ..._releasesCache.data, currentVersion };
  }
  try {
    const buf = await downloadHttpsBuffer('https://api.github.com/repos/Wlaeg/antigravity-pet/releases?per_page=8', 2 * 1024 * 1024, 10000);
    const list = JSON.parse(buf.toString('utf8'));
    const releases = Array.isArray(list) ? list.filter((r) => r && !r.draft).map((r) => ({
      tag: String(r.tag_name || ''),
      name: String(r.name || r.tag_name || ''),
      body: String(r.body || ''),
      publishedAt: String(r.published_at || r.created_at || ''),
      htmlUrl: String(r.html_url || ''),
      prerelease: Boolean(r.prerelease),
      assets: Array.isArray(r.assets) ? r.assets.map((a) => ({
        name: String(a.name || ''),
        size: Number(a.size || 0),
        downloadUrl: String(a.browser_download_url || '')
      })) : []
    })) : [];
    const result = {
      ok: true,
      currentVersion,
      repoUrl: 'https://github.com/Wlaeg/antigravity-pet/releases',
      releases
    };
    _releasesCache = { ts: now, data: result };
    return result;
  } catch (err) {
    return {
      ok: false,
      currentVersion,
      repoUrl: 'https://github.com/Wlaeg/antigravity-pet/releases',
      error: err && err.message ? err.message : String(err),
      releases: _releasesCache.data ? _releasesCache.data.releases : []
    };
  }
}

async function loadSidecarSdk() {
  const candidates = [
    () => import('sidecar_sdk'),
    () => import(pathToFileURL(path.join(os.homedir(), '.gemini', 'antigravity', 'lib', 'node_modules', 'sidecar_sdk', 'index.mjs')).href),
    () => import(pathToFileURL(path.join(os.homedir(), '.gemini', 'config', 'node_modules', 'sidecar_sdk', 'index.mjs')).href)
  ];
  for (const loader of candidates) {
    try {
      const mod = await loader();
      if (mod?.SidecarApp) return mod.SidecarApp;
    } catch {
      // try next
    }
  }
  return null;
}

const isDirectRun = process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url));

if (isDirectRun) {
  const forceStandalone = process.argv.includes('--standalone');
  const SidecarApp = forceStandalone ? null : await loadSidecarSdk();
  const hasSidecarEnv = Boolean(
    !forceStandalone &&
    SidecarApp &&
    process.env.ANTIGRAVITY_LS_ADDRESS &&
    process.env.ANTIGRAVITY_CSRF_TOKEN &&
    process.env.ANTIGRAVITY_SIDECAR_WEB_PORT &&
    process.env.ANTIGRAVITY_SIDECAR_UI_TOKEN
  );

  if (hasSidecarEnv) {
    void ensurePetLaunchedWithAgy();
    const app = new SidecarApp();
    startLocalDispatchBridge(app);
    app.page('/', () => fs.readFileSync(HTML_PATH, 'utf8'));
    app.api('/api/status', async () => await getSnapshot(), 'POST');
    app.api('/api/apply', async (data) => await applySetting(data || {}), 'POST');
    app.api('/api/import-pet', async (data) => await importPetPack(data || {}), 'POST');
    app.api('/api/community-pets', async () => await getCommunityPets(), 'POST');
    app.api('/api/install-community-pet', async (data) => await installCommunityPet(data || {}), 'POST');
    app.api('/api/star-community-pet', async (data) => await toggleCommunityStar(data || {}), 'POST');
    app.api('/api/delete-pet', async (data) => await deleteCustomPet(data || {}), 'POST');
    app.api('/api/pet-avatars', async () => await getPetAvatars(), 'POST');
    app.api('/api/github-releases', async (data) => await getGithubReleases(Boolean(data && data.force)), 'POST');
    await app.run();
    console.log('[antigravity-pet panel] sidecar registered and running');
  } else {
    const server = http.createServer(async (req, res) => {
      const url = new URL(req.url, 'http://127.0.0.1');
      if (req.method === 'GET' && url.pathname === '/api/ping') {
        const addr = server.address();
        res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
        res.end(JSON.stringify({ ok: true, app: 'antigravity-pet-panel', port: addr ? addr.port : 0 }));
        return;
      }
      if (req.method === 'GET' && url.pathname === '/') {
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
        res.end(fs.readFileSync(HTML_PATH, 'utf8'));
        return;
      }
      if (req.method === 'GET' && url.pathname === '/preload.js') {
        res.writeHead(200, { 'Content-Type': 'application/javascript; charset=utf-8' });
        res.end('');
        return;
      }
      if (req.method === 'POST' && (
        url.pathname === '/api/status' ||
        url.pathname === '/api/apply' ||
        url.pathname === '/api/import-pet' ||
        url.pathname === '/api/community-pets' ||
        url.pathname === '/api/install-community-pet' ||
        url.pathname === '/api/star-community-pet' ||
        url.pathname === '/api/delete-pet' ||
        url.pathname === '/api/pet-avatars' ||
        url.pathname === '/api/github-releases'
      )) {
        let raw = '';
        req.on('data', (c) => (raw += c));
        req.on('end', async () => {
          let data = {};
          try { if (raw) data = JSON.parse(raw); } catch {}
          let result;
          if (url.pathname === '/api/status') result = await getSnapshot();
          else if (url.pathname === '/api/import-pet') result = await importPetPack(data);
          else if (url.pathname === '/api/community-pets') result = await getCommunityPets();
          else if (url.pathname === '/api/install-community-pet') result = await installCommunityPet(data);
          else if (url.pathname === '/api/star-community-pet') result = await toggleCommunityStar(data);
          else if (url.pathname === '/api/delete-pet') result = await deleteCustomPet(data);
          else if (url.pathname === '/api/pet-avatars') result = await getPetAvatars();
          else if (url.pathname === '/api/github-releases') result = await getGithubReleases(Boolean(data && data.force));
          else result = await applySetting(data);
          res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
          res.end(JSON.stringify(result));
        });
        return;
      }
      res.writeHead(404);
      res.end('Not Found');
    });
    const preferredPort = Number(process.env.PET_PANEL_PORT || 47822);
    let retriedRandomPort = false;
    server.on('error', (err) => {
      if (err && err.code === 'EADDRINUSE' && !retriedRandomPort && preferredPort !== 0) {
        retriedRandomPort = true;
        server.listen(0, '127.0.0.1');
        return;
      }
      console.error('[antigravity-pet panel] standalone server error:', err && err.message ? err.message : err);
    });
    server.listen(preferredPort, '127.0.0.1', () => {
      const addr = server.address();
      const infoPath = path.join(path.dirname(getConfigPath()), 'panel-server.json');
      try {
        fs.writeFileSync(infoPath, JSON.stringify({
          port: addr.port,
          pid: process.pid,
          url: `http://127.0.0.1:${addr.port}/`,
          updatedAt: new Date().toISOString()
        }, null, 2), 'utf8');
      } catch {}
      console.log(`[antigravity-pet panel] standalone listening on http://127.0.0.1:${addr.port}`);
    });
  }
}
