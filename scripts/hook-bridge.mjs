import { execFile, spawn } from 'node:child_process';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { formatToolLabel } from './tool-label-mapper.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PLUGIN_ROOT = path.resolve(__dirname, '..');
const IS_WIN = process.platform === 'win32';
const IS_MAC = process.platform === 'darwin';
const EXE_PATH = path.join(PLUGIN_ROOT, 'app', 'AntigravityPet.exe');
const MAC_APP_BUNDLE = path.join(PLUGIN_ROOT, 'app', 'AntigravityPet.app');
const MAC_BIN_PATH = path.join(MAC_APP_BUNDLE, 'Contents', 'MacOS', 'AntigravityPet');
const MAC_BUILD_SH = path.join(PLUGIN_ROOT, 'build.sh');
const PORT = 47821;
const TASK_NAME = 'AntigravityPetLauncher';

function readStdin(timeoutMs = 200) {
  return new Promise((resolve) => {
    let data = '';
    const timer = setTimeout(() => resolve(data), timeoutMs);
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => {
      data += chunk;
    });
    process.stdin.on('end', () => {
      clearTimeout(timer);
      resolve(data);
    });
    process.stdin.on('error', () => {
      clearTimeout(timer);
      resolve(data);
    });
  });
}

function readHttpToken() {
  try {
    const bridgePath = path.join(os.homedir(), '.antigravity-pet', 'http-bridge.json');
    if (!fs.existsSync(bridgePath)) return '';
    const data = JSON.parse(fs.readFileSync(bridgePath, 'utf8').replace(/^\uFEFF/, ''));
    return typeof data?.token === 'string' ? data.token : '';
  } catch {
    return '';
  }
}

function postJson(urlPath, payload, timeoutMs = 250, extraHeaders = null) {
  return new Promise((resolve) => {
    const body = JSON.stringify(payload);
    const req = http.request(
      {
        hostname: '127.0.0.1',
        port: PORT,
        path: urlPath,
        method: 'POST',
        headers: {
          'Content-Type': 'application/json; charset=utf-8',
          'Content-Length': Buffer.byteLength(body),
          ...(extraHeaders || {})
        },
        timeout: timeoutMs
      },
      (res) => {
        let resp = '';
        res.setEncoding('utf8');
        res.on('data', (c) => { resp += c; });
        res.on('end', () => {
          try {
            resolve(resp ? JSON.parse(resp) : true);
          } catch {
            resolve(true);
          }
        });
      }
    );
    req.on('timeout', () => {
      req.destroy();
      resolve(false);
    });
    req.on('error', () => resolve(false));
    req.write(body);
    req.end();
  });
}

function checkHealth(timeoutMs = 200) {
  return new Promise((resolve) => {
    const req = http.get(
      { hostname: '127.0.0.1', port: PORT, path: '/health', timeout: timeoutMs },
      (res) => {
        res.resume();
        resolve(res.statusCode === 200);
      }
    );
    req.on('timeout', () => {
      req.destroy();
      resolve(false);
    });
    req.on('error', () => resolve(false));
  });
}

function getConfigPath() {
  if (IS_MAC) {
    return path.join(os.homedir(), 'Library', 'Application Support', 'AntigravityPet', 'config.json');
  }
  if (IS_WIN) {
    const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local');
    return path.join(localAppData, 'AntigravityPet', 'config.json');
  }
  return path.join(os.homedir(), '.config', 'AntigravityPet', 'config.json');
}

function isPetEnabledInConfig() {
  try {
    const cfgPath = getConfigPath();
    if (!fs.existsSync(cfgPath)) return true;
    const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8').replace(/^\uFEFF/, ''));
    return cfg.Enabled !== false && cfg.AutoStartOnBoot !== false;
  } catch {
    return true;
  }
}

function launchPetInRealDesktop() {
  return new Promise((resolve) => {
    if (IS_MAC) {
      if (fs.existsSync(MAC_BIN_PATH)) {
        execFile('open', ['-g', '-a', MAC_APP_BUNDLE], { timeout: 1200 }, (err) => {
          if (err) {
            try {
              const child = spawn(MAC_BIN_PATH, [], { detached: true, stdio: 'ignore' });
              child.unref();
            } catch {}
          }
          resolve();
        });
      } else if (fs.existsSync(MAC_BUILD_SH)) {
        execFile('bash', [MAC_BUILD_SH, '--launch'], { timeout: 25000 }, () => resolve());
      } else {
        resolve();
      }
      return;
    }

    execFile('schtasks', ['/Run', '/TN', TASK_NAME], { timeout: 600, windowsHide: true }, (err) => {
      if (err) {
        try {
          const child = spawn(EXE_PATH, [], {
            detached: true,
            stdio: 'ignore',
            windowsHide: false
          });
          child.unref();
        } catch {
          // ignore
        }
      }
      resolve();
    });
  });
}

function extractTitle(payload) {
  const raw = String(payload.lastUserInput || payload.taskTitle || payload.title || '').replace(/\s+/g, ' ').trim();
  if (!raw) return '';
  return raw.length > 24 ? `${raw.slice(0, 24)}…` : raw;
}

function detectBackground(payload) {
  if (!payload || typeof payload !== 'object') return false;
  if (payload.isBackground === true || payload.background === true) return true;
  const mode = String(payload.mode || payload.runMode || '').toLowerCase();
  if (mode === 'background' || mode === 'daemon') return true;
  const src = String(payload.hookSource || payload.source || payload.trigger || '').toLowerCase();
  return /background|daemon|cron|subagent|schedule/.test(src);
}

async function main() {
  let payload = {};
  try {
    const raw = await readStdin();
    if (raw && raw.trim()) {
      payload = JSON.parse(raw);
    }
  } catch {
    payload = {};
  }

  const evt = payload.hookEventName || '';
  const conversationId = payload.conversationId || 'default';
  const parentConversationId = payload.parentConversationId || payload.parentId || '';
  const title = extractTitle(payload);
  const isBackground = detectBackground(payload);
  const activeState = isBackground ? 'background_running' : 'running';

  if (evt === 'SessionStart') {
    const alive = await checkHealth();
    if (!alive) {
      if (isPetEnabledInConfig()) {
        await launchPetInRealDesktop();
      }
    } else if (!isBackground) {
      await postJson('/state', { conversationId, state: 'idle', message: '', title });
    }
  } else if (evt === 'PreInvocation') {
    let alive = await checkHealth();
    if (!alive && isPetEnabledInConfig()) {
      await launchPetInRealDesktop();
      await new Promise((r) => setTimeout(r, 350));
      alive = await checkHealth();
    }
    if (alive) {
      await postJson('/state', {
        conversationId,
        parentConversationId,
        isBackground,
        state: activeState,
        message: '正在思考',
        messageEn: 'Thinking',
        toolPhase: 'thinking',
        title
      });
    }
  } else if (evt === 'PostToolUse') {
    if (payload.error) {
      if (isBackground) {
        await postJson('/state', {
          conversationId,
          parentConversationId,
          isBackground: true,
          state: 'idle',
          backgroundDone: true,
          backgroundSuccess: false,
          message: '工具出错',
          messageEn: 'Tool error',
          toolPhase: 'error',
          title,
          isToolCall: true
        });
      } else {
        await postJson('/state', {
          conversationId,
          state: 'blocked',
          message: '工具出错 · 点击查看',
          messageEn: 'Blocked · Click to view',
          toolPhase: 'error',
          title,
          isToolCall: true
        });
      }
    } else {
      await postJson('/state', {
        conversationId,
        parentConversationId,
        isBackground,
        state: activeState,
        message: '',
        messageEn: '',
        toolPhase: 'end',
        title
      });
    }
  } else if (evt === 'PostInvocation') {
    await postJson('/state', {
      conversationId,
      parentConversationId,
      isBackground,
      state: activeState,
      message: '整理回复中',
      messageEn: 'Processing results',
      toolPhase: 'finishing',
      title
    });
  } else if (evt === 'PreToolUse') {
    const toolCall = payload.toolCall || { name: payload.toolName || 'tool', args: {} };
    const toolName = toolCall.name || payload.toolName || 'tool';
    const isRunCommand = toolName === 'run_command';
    const cmdLine = String((toolCall.args && (toolCall.args.CommandLine || toolCall.args.command)) || '');
    const isHighRisk = isRunCommand && /(\brm\s+-rf?\b|\bRemove-Item\b|\bdel\s+\/|\bformat\s+|\bgit\s+push\b|\bgit\s+reset\s+--hard\b|\bshutdown\b|\breg\s+delete\b)/i.test(cmdLine);

    if (isHighRisk) {
      const alive = await checkHealth(150);
      if (alive) {
        const token = readHttpToken();
        const headers = token ? { 'X-Pet-Token': token } : null;
        const res = await postJson('/approval', {
          conversationId,
          toolName,
          summary: cmdLine.slice(0, 120),
          ...(token ? { token } : {})
        }, 46000, headers);
        if (res && typeof res === 'object' && (res.decision === 'allow' || res.decision === 'deny' || res.decision === 'ask')) {
          const out = { decision: res.decision };
          if (res.decision === 'deny') out.reason = '用户在桌面宠物气泡上拒绝了此操作';
          process.stdout.write(JSON.stringify(out));
          return;
        }
      }
    }

    const label = formatToolLabel(toolCall);
    await postJson('/state', {
      conversationId,
      parentConversationId,
      isBackground,
      state: activeState,
      message: label.zh,
      messageEn: label.en,
      toolPhase: 'start',
      title,
      isToolCall: true
    });

    if (isRunCommand) {
      process.stdout.write(JSON.stringify({ decision: 'ask' }));
    } else {
      process.stdout.write(JSON.stringify({ decision: 'allow' }));
    }
    return;
  } else if (evt === 'Stop') {
    const reason = payload.terminationReason || '';
    const isErr = reason === 'ERROR' || Boolean(payload.error);
    if (isBackground) {
      await postJson('/state', {
        conversationId,
        parentConversationId,
        isBackground: true,
        state: 'idle',
        backgroundDone: true,
        backgroundSuccess: !isErr,
        message: isErr ? '后台任务出错' : '后台任务已完成',
        messageEn: isErr ? 'Bg task failed' : 'Bg task done',
        title
      });
    } else if (isErr) {
      await postJson('/state', { conversationId, state: 'blocked', message: '任务出错 · 点击查看', messageEn: 'Task error · Click to view', title });
    } else if (reason === 'USER_CANCELED') {
      await postJson('/state', { conversationId, state: 'idle', message: '', title });
    } else {
      await postJson('/state', { conversationId, state: 'ready', message: '任务完成!', messageEn: '✓ Task Ready · Click', title });
    }
  }

  process.stdout.write('{}');
}

main().catch(() => {
  process.stdout.write('{}');
});
