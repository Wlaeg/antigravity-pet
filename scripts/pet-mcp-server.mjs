import { execFile, spawn } from 'node:child_process';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { getCommunityPets, importPetPack, installCommunityPet } from '../sidecars/panel/server.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PLUGIN_ROOT = path.resolve(__dirname, '..');
const IS_WIN = process.platform === 'win32';
const IS_MAC = process.platform === 'darwin';
const EXE_PATH = path.join(PLUGIN_ROOT, 'app', 'AntigravityPet.exe');
const MAC_APP_BUNDLE = path.join(PLUGIN_ROOT, 'app', 'AntigravityPet.app');
const MAC_BIN_PATH = path.join(MAC_APP_BUNDLE, 'Contents', 'MacOS', 'AntigravityPet');
const MAC_BUILD_SH = path.join(PLUGIN_ROOT, 'build.sh');
const UI_HTML_PATH = path.join(PLUGIN_ROOT, 'ui', 'settings.html');
const PETS_DIR = path.join(PLUGIN_ROOT, 'pets');
const USER_PETS_DIR = path.join(os.homedir(), '.antigravity-pet', 'pets');
const PORT = 47821;
const TASK_NAME = 'AntigravityPetLauncher';
const RESOURCE_URI = 'ui://antigravity-pet/settings.html';

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
    ToolCallsCount: 0
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

async function waitForHealth(maxWaitMs = 2600) {
  const start = Date.now();
  while (Date.now() - start < maxWaitMs) {
    const res = await httpRequest('GET', '/health', null, 400);
    if (res.ok && res.data) return res.data;
    await new Promise((r) => setTimeout(r, 250));
  }
  return null;
}

async function getSnapshot() {
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

async function applySetting(args = {}) {
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
  } else if (action === 'wave' || action === 'jump' || action === 'walk') {
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
  } else if (action === 'communityPets') {
    const comm = await getCommunityPets();
    const snap = await getSnapshot();
    return { ...snap, communityPets: comm.pets, communitySource: comm.source };
  } else if (action === 'installCommunityPet') {
    const res = await installCommunityPet({ id: args.id || value, zipUrl: args.zipUrl, localZip: args.localZip });
    const comm = await getCommunityPets();
    const snap = res.snapshot || await getSnapshot();
    return { ...snap, importResult: res, communityPets: comm.pets, communitySource: comm.source };
  } else if (action === 'importPet') {
    const res = await importPetPack({
      zipBase64: args.zipBase64,
      zipUrl: args.zipUrl,
      petJson: args.petJson,
      spritesheetBase64: args.spritesheetBase64
    });
    const snap = res.snapshot || await getSnapshot();
    return { ...snap, importResult: res };
  } else {
    const patch = {};
    if (args.skin !== undefined) patch.Skin = String(args.skin);
    if (args.scale !== undefined) patch.Scale = Number(args.scale);
    if (args.bubbleMode !== undefined) patch.BubbleMode = String(args.bubbleMode);
    if (args.language !== undefined) patch.Language = String(args.language).toLowerCase();
    if (args.autoWalk !== undefined) patch.AutoWalk = parseBool(args.autoWalk, true);
    if (args.eyeFollow !== undefined) patch.EyeFollow = parseBool(args.eyeFollow, true);
    if (args.topmost !== undefined) patch.Topmost = parseBool(args.topmost, true);
    if (args.autoStartOnBoot !== undefined) {
      patch.AutoStartOnBoot = parseBool(args.autoStartOnBoot, true);
      await syncRegistryAutoStart(patch.AutoStartOnBoot);
    }
    if (args.soundAlert !== undefined) patch.SoundAlert = parseBool(args.soundAlert, false);
    if (args.checkUpdatesOnStartup !== undefined) patch.CheckUpdatesOnStartup = parseBool(args.checkUpdatesOnStartup, true);
    if (Object.keys(patch).length > 0) {
      writeOfflineConfig(patch);
      if (isRunning) {
        await httpRequest('POST', '/control', {
          action: 'updateConfig',
          skin: patch.Skin,
          scale: patch.Scale,
          bubbleMode: patch.BubbleMode,
          language: patch.Language,
          autoWalk: patch.AutoWalk,
          eyeFollow: patch.EyeFollow,
          topmost: patch.Topmost,
          autoStartOnBoot: patch.AutoStartOnBoot,
          checkUpdatesOnStartup: patch.CheckUpdatesOnStartup,
          soundAlert: patch.SoundAlert
        });
      }
    }
  }

  return await getSnapshot();
}

const TOOLS = [
  {
    name: 'pet_settings',
    title: 'Desktop Pet Settings / 桌面宠物设置',
    description: 'Open the Antigravity Desktop Pet settings panel (toggle pet, switch 6 pixel pets, language, size, bubble mode, autostart, and jump to chats).',
    inputSchema: {
      type: 'object',
      properties: {
        action: { type: 'string', description: 'Optional: start/show/hide/quit/toggle/status/resetPos/openConversation' },
        skin: { type: 'string', description: 'Pet ID: cyber-fox/astro-bot/shadow-cat/byte-ghost/shiba-dog/pixel-dragon/android-bugdroid/flutter-dash' },
        scale: { type: 'number', description: 'Scale factor: 0.33 to 3.0 (default 1.0)' },
        bubbleMode: { type: 'string', description: 'Bubble mode: always/alerts/off' },
        language: { type: 'string', description: 'Language: auto/en/zh' },
        autoWalk: { type: 'boolean', description: 'Idle roaming toggle' },
        eyeFollow: { type: 'boolean', description: '16-direction eye follow cursor toggle' },
        topmost: { type: 'boolean', description: 'Always on top toggle' },
        autoStartOnBoot: { type: 'boolean', description: 'Launch at login toggle' },
        soundAlert: { type: 'boolean', description: 'Completion sound alert toggle' }
      }
    },
    _meta: {
      ui: {
        resourceUri: RESOURCE_URI,
        visibility: ['model', 'app']
      }
    }
  },
  {
    name: 'pet_apply_setting',
    title: '应用桌宠设置',
    description: '直接开启/关闭/修改桌面宠物配置与状态，并同步保存。',
    inputSchema: {
      type: 'object',
      properties: {
        action: { type: 'string' },
        value: { type: 'string' },
        state: { type: 'string' },
        message: { type: 'string' }
      }
    },
    _meta: {
      ui: {
        visibility: ['model', 'app']
      }
    }
  }
];

let useContentLengthFraming = null;

function sendMessage(msg) {
  const json = JSON.stringify(msg);
  if (useContentLengthFraming) {
    const bytes = Buffer.byteLength(json, 'utf8');
    process.stdout.write(`Content-Length: ${bytes}\r\n\r\n${json}`);
  } else {
    process.stdout.write(`${json}\n`);
  }
}

async function handleRpc(req) {
  const { id, method, params } = req;
  if (!method) return;

  // 通知无需回复
  if (id === undefined || id === null) return;

  try {
    if (method === 'initialize') {
      const proto = params?.protocolVersion || '2025-03-26';
      sendMessage({
        jsonrpc: '2.0',
        id,
        result: {
          protocolVersion: proto,
          capabilities: {
            tools: { listChanged: false },
            resources: { subscribe: false, listChanged: false }
          },
          serverInfo: {
            name: 'antigravity-pet-mcp',
            version: '3.0.0'
          }
        }
      });
    } else if (method === 'ping') {
      sendMessage({ jsonrpc: '2.0', id, result: {} });
    } else if (method === 'resources/list') {
      sendMessage({
        jsonrpc: '2.0',
        id,
        result: {
          resources: [
            {
              uri: RESOURCE_URI,
              name: 'Antigravity 桌面宠物设置面板',
              description: '可视化控制桌面宠物开关、皮肤、尺寸、气泡、随 Antigravity 启动与最近对话跳转',
              mimeType: 'text/html;profile=mcp-app',
              _meta: { ui: { prefersBorder: false } }
            }
          ]
        }
      });
    } else if (method === 'resources/read') {
      const uri = params?.uri;
      if (uri !== RESOURCE_URI) {
        sendMessage({ jsonrpc: '2.0', id, error: { code: -32602, message: `Unknown resource: ${uri}` } });
        return;
      }
      const html = fs.readFileSync(UI_HTML_PATH, 'utf8');
      sendMessage({
        jsonrpc: '2.0',
        id,
        result: {
          contents: [
            {
              uri: RESOURCE_URI,
              mimeType: 'text/html;profile=mcp-app',
              text: html,
              _meta: { ui: { prefersBorder: false } }
            }
          ]
        }
      });
    } else if (method === 'tools/list') {
      sendMessage({ jsonrpc: '2.0', id, result: { tools: TOOLS } });
    } else if (method === 'tools/call') {
      const name = params?.name;
      const args = params?.arguments || {};
      if (name !== 'pet_settings' && name !== 'pet_apply_setting') {
        sendMessage({ jsonrpc: '2.0', id, error: { code: -32601, message: `Unknown tool: ${name}` } });
        return;
      }
      const snapshot = await applySetting(args);
      const summary = `桌宠当前${snapshot.running ? (snapshot.visible ? '运行中并已显示' : '运行中（已隐藏）') : '已关闭'}；角色=${snapshot.displayName || snapshot.skin}；状态=${snapshot.state}；尺寸=${snapshot.scale}x；气泡=${snapshot.bubbleMode}；随AGY启动=${snapshot.autoStartOnBoot ? '开' : '关'}`;
      sendMessage({
        jsonrpc: '2.0',
        id,
        result: {
          content: [{ type: 'text', text: summary }],
          structuredContent: snapshot
        }
      });
    } else {
      sendMessage({ jsonrpc: '2.0', id, error: { code: -32601, message: `Method not found: ${method}` } });
    }
  } catch (err) {
    sendMessage({
      jsonrpc: '2.0',
      id,
      error: { code: -32603, message: String(err?.message || err) }
    });
  }
}

let rxBuffer = Buffer.alloc(0);

function processBuffer() {
  while (rxBuffer.length > 0) {
    const strHead = rxBuffer.slice(0, Math.min(rxBuffer.length, 64)).toString('utf8');
    if (strHead.startsWith('Content-Length:')) {
      useContentLengthFraming = true;
      const sepIdx = rxBuffer.indexOf('\r\n\r\n');
      if (sepIdx === -1) return;
      const headerText = rxBuffer.slice(0, sepIdx).toString('utf8');
      const m = /Content-Length:\s*(\d+)/i.exec(headerText);
      if (!m) {
        rxBuffer = rxBuffer.slice(sepIdx + 4);
        continue;
      }
      const len = parseInt(m[1], 10);
      const total = sepIdx + 4 + len;
      if (rxBuffer.length < total) return;
      const jsonText = rxBuffer.slice(sepIdx + 4, total).toString('utf8');
      rxBuffer = rxBuffer.slice(total);
      try {
        void handleRpc(JSON.parse(jsonText));
      } catch {
        // ignore
      }
    } else {
      const nlIdx = rxBuffer.indexOf('\n');
      if (nlIdx === -1) return;
      if (useContentLengthFraming === null) useContentLengthFraming = false;
      const line = rxBuffer.slice(0, nlIdx).toString('utf8').trim();
      rxBuffer = rxBuffer.slice(nlIdx + 1);
      if (!line) continue;
      try {
        void handleRpc(JSON.parse(line));
      } catch {
        // ignore
      }
    }
  }
}

process.stdin.on('data', (chunk) => {
  rxBuffer = Buffer.concat([rxBuffer, Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk, 'utf8')]);
  processBuffer();
});

process.stdin.on('end', () => {
  process.exit(0);
});

const isDirectRun = process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url));
if (isDirectRun) {
  void ensurePetLaunchedWithAgy();
}
