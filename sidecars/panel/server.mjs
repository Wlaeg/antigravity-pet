import { execFile, spawn } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import http from 'node:http';
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
    Accessory: 'auto'
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

function detectEffectiveLang(cfg) {
  const mode = String(cfg?.Language || 'auto').trim().toLowerCase();
  if (mode === 'en' || mode.startsWith('en-')) return 'en';
  if (mode === 'zh' || mode.startsWith('zh-')) return 'zh';
  const agy = String(cfg?.AgyDetectedLocale || '').trim().toLowerCase();
  if (agy.startsWith('en')) return 'en';
  if (agy.startsWith('zh')) return 'zh';
  try {
    const agyDataDir = IS_MAC
      ? path.join(os.homedir(), 'Library', 'Application Support', 'Antigravity')
      : path.join(process.env.APPDATA || path.join(os.homedir(), 'AppData', 'Roaming'), 'Antigravity');
    const sp = path.join(agyDataDir, 'app_storage.json');
    if (fs.existsSync(sp)) {
      const raw = fs.readFileSync(sp, 'utf8');
      if (/"(?:locale|language)":"zh/i.test(raw)) return 'zh';
      if (/"(?:locale|language)":"en/i.test(raw)) return 'en';
    }
    const lp = path.join(agyDataDir, 'localization-prefs.json');
    if (fs.existsSync(lp)) {
      const raw = fs.readFileSync(lp, 'utf8');
      if (/"useTw"\s*:\s*true/i.test(raw) || /chinese/i.test(raw)) return 'zh';
      if (/english/i.test(raw)) return 'en';
    }
  } catch {}
  return 'en';
}

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
          map.set(id, {
            id,
            displayName: (lang === 'en' && enInfo) ? enInfo.name : (meta.displayName || id),
            description: (lang === 'en' && enInfo) ? enInfo.desc : (meta.description || ''),
            version: meta.spriteVersionNumber || 2
          });
        } catch {
          // ignore
        }
      }
    } catch {
      // ignore
    }
  }
  return Array.from(map.values()).sort((a, b) => a.displayName.localeCompare(b.displayName, lang === 'en' ? 'en' : 'zh-CN'));
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

export async function getSnapshot() {
  const res = await httpRequest('GET', '/health', null, 600);
  if (res.ok && res.data) {
    return res.data;
  }
  const cfg = readOfflineConfig();
  const effLang = detectEffectiveLang(cfg);
  return {
    ok: true,
    running: false,
    enabled: cfg.Enabled !== false,
    state: 'idle',
    skin: cfg.Skin || 'cyber-fox',
    displayName: cfg.Skin || 'cyber-fox',
    visible: false,
    scale: cfg.Scale || 1.0,
    bubbleMode: cfg.BubbleMode || 'always',
    bubbleStyle: cfg.BubbleStyle || 'simple',
    pinnedConversationId: cfg.PinnedConversationId || '',
    readyTimeoutSec: typeof cfg.ReadyTimeoutSec === 'number' ? cfg.ReadyTimeoutSec : 30,
    snapToAgy: !!cfg.SnapToAgy,
    hideOnFullscreen: cfg.HideOnFullscreen !== false,
    accessory: cfg.Accessory || 'auto',
    effectiveAccessory: cfg.Accessory && cfg.Accessory !== 'auto' ? cfg.Accessory : (cfg.CompletedTasksCount >= 100 ? 'crown' : cfg.CompletedTasksCount >= 50 ? 'coffee' : cfg.CompletedTasksCount >= 20 ? 'cap' : cfg.CompletedTasksCount >= 5 ? 'glasses' : 'none'),
    autoWalk: cfg.AutoWalk !== false,
    eyeFollow: cfg.EyeFollow !== false,
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
    const acc = String(value || args.accessory || 'auto').toLowerCase();
    writeOfflineConfig({ Accessory: acc });
    if (isRunning) await httpRequest('POST', '/control', { action: 'accessory', value: acc });
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
      message: String(args.message || '')
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

export async function importPetPack(args = {}) {
  const tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'agy-pet-import-'));
  try {
    let sourceDir = null;
    if (args.zipBase64) {
      const cleanB64 = String(args.zipBase64).replace(/^data:[^;]+;base64,/, '');
      const zipPath = path.join(tmpRoot, 'pack.zip');
      const extractDir = path.join(tmpRoot, 'extracted');
      fs.writeFileSync(zipPath, Buffer.from(cleanB64, 'base64'));
      fs.mkdirSync(extractDir, { recursive: true });
      await new Promise((resolve, reject) => {
        if (IS_WIN) {
          execFile('powershell.exe', ['-NoProfile', '-Command', `Expand-Archive -LiteralPath '${zipPath.replace(/'/g, "''")}' -DestinationPath '${extractDir.replace(/'/g, "''")}' -Force`], { timeout: 15000, windowsHide: true }, (err) => (err ? reject(err) : resolve()));
        } else {
          execFile('unzip', ['-o', zipPath, '-d', extractDir], { timeout: 15000 }, (err) => (err ? reject(err) : resolve()));
        }
      });
      sourceDir = findPetManifestDir(extractDir);
    } else if (args.localPath) {
      const lp = path.resolve(String(args.localPath));
      if (fs.existsSync(lp) && fs.statSync(lp).isDirectory()) {
        sourceDir = findPetManifestDir(lp);
      } else if (fs.existsSync(lp) && lp.toLowerCase().endsWith('.zip')) {
        const extractDir = path.join(tmpRoot, 'extracted');
        fs.mkdirSync(extractDir, { recursive: true });
        await new Promise((resolve, reject) => {
          if (IS_WIN) {
            execFile('powershell.exe', ['-NoProfile', '-Command', `Expand-Archive -LiteralPath '${lp.replace(/'/g, "''")}' -DestinationPath '${extractDir.replace(/'/g, "''")}' -Force`], { timeout: 15000, windowsHide: true }, (err) => (err ? reject(err) : resolve()));
          } else {
            execFile('unzip', ['-o', lp, '-d', extractDir], { timeout: 15000 }, (err) => (err ? reject(err) : resolve()));
          }
        });
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
    if (!hasSheet && !hasIdleFrames) {
      return { ok: false, error: '宠物包缺少 spritesheet.png 或 idle/ 序列帧目录' };
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
  const SidecarApp = await loadSidecarSdk();
  const hasSidecarEnv = Boolean(
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
    await app.run();
    console.log('[antigravity-pet panel] sidecar registered and running');
  } else {
    const server = http.createServer(async (req, res) => {
      const url = new URL(req.url, 'http://127.0.0.1');
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
      if (req.method === 'POST' && (url.pathname === '/api/status' || url.pathname === '/api/apply' || url.pathname === '/api/import-pet')) {
        let raw = '';
        req.on('data', (c) => (raw += c));
        req.on('end', async () => {
          let data = {};
          try { if (raw) data = JSON.parse(raw); } catch {}
          const result = url.pathname === '/api/status'
            ? await getSnapshot()
            : (url.pathname === '/api/import-pet' ? await importPetPack(data) : await applySetting(data));
          res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
          res.end(JSON.stringify(result));
        });
        return;
      }
      res.writeHead(404);
      res.end('Not Found');
    });
    const port = Number(process.env.PET_PANEL_PORT || 0);
    server.listen(port, '127.0.0.1', () => {
      const addr = server.address();
      console.log(`[antigravity-pet panel] standalone listening on http://127.0.0.1:${addr.port}`);
    });
  }
}
