import { execFile, spawn } from 'node:child_process';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const EXE_PATH = path.resolve(__dirname, '..', 'app', 'AntigravityPet.exe');
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

function postJson(urlPath, payload, timeoutMs = 250) {
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
          'Content-Length': Buffer.byteLength(body)
        },
        timeout: timeoutMs
      },
      (res) => {
        res.resume();
        res.on('end', () => resolve(true));
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

function isPetEnabledInConfig() {
  try {
    const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local');
    const cfgPath = path.join(localAppData, 'AntigravityPet', 'config.json');
    if (!fs.existsSync(cfgPath)) return true;
    const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8').replace(/^\uFEFF/, ''));
    return cfg.Enabled !== false && cfg.AutoStartOnBoot !== false;
  } catch {
    return true;
  }
}

function launchPetInRealDesktop() {
  return new Promise((resolve) => {
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
  const raw = String(payload.lastUserInput || '').replace(/\s+/g, ' ').trim();
  if (!raw) return '';
  return raw.length > 24 ? `${raw.slice(0, 24)}…` : raw;
}

function friendlyToolLabel(toolCall) {
  const name = toolCall?.name || '';
  const args = toolCall?.args || {};

  if (name === 'call_mcp_tool') {
    const server = String(args.ServerName || '').toLowerCase();
    const mcpTool = String(args.ToolName || '').toLowerCase();
    if (server.includes('windows-mcp')) {
      if (mcpTool === 'screenshot' || mcpTool === 'snapshot') return '查看屏幕中...';
      if (mcpTool === 'app') return '操作桌面应用中...';
      return '操控桌面中...';
    }
    if (server.includes('agy-dart_fast')) {
      if (mcpTool === 'apply_edits') return '编辑代码中...';
      if (mcpTool === 'batch_search') return '检索代码中...';
      if (mcpTool === 'run_checks') return '运行检查中...';
      return '快速分析代码中...';
    }
    if (server.includes('agy-dart_context')) {
      return '分析长文本中...';
    }
    if (server.includes('antigravity-pet')) {
      return '更新桌宠设置...';
    }
    return '调用扩展工具中...';
  }

  const map = {
    run_command: '执行命令中...',
    read_file: '读取文件中...',
    write_file: '编写文件中...',
    edit_file: '修改代码中...',
    search_web: '搜索资料中...',
    invoke_subagent: '调度子代理中...',
    ask_question: '等待你回答...',
    manage_task: '管理后台任务...',
    exit_plan_mode: '准备开始执行...'
  };
  return map[name] || '执行任务中...';
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
  const title = extractTitle(payload);

  if (evt === 'SessionStart') {
    const alive = await checkHealth();
    if (!alive) {
      if (isPetEnabledInConfig()) {
        await launchPetInRealDesktop();
      }
    } else {
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
      await postJson('/state', { conversationId, state: 'running', message: '思考规划中...', title });
    }
  } else if (evt === 'PostToolUse') {
    if (payload.error) {
      await postJson('/state', {
        conversationId,
        state: 'blocked',
        message: '工具出错 · 点击查看',
        title,
        isToolCall: true
      });
    } else {
      await postJson('/state', {
        conversationId,
        state: 'running',
        message: friendlyToolLabel(payload.toolCall),
        title,
        isToolCall: true
      });
    }
  } else if (evt === 'PostInvocation') {
    await postJson('/state', { conversationId, state: 'running', message: '处理结果中...', title });
  } else if (evt === 'Stop') {
    const reason = payload.terminationReason || '';
    if (reason === 'ERROR' || payload.error) {
      await postJson('/state', { conversationId, state: 'blocked', message: '任务出错 · 点击查看', title });
    } else if (reason === 'USER_CANCELED') {
      await postJson('/state', { conversationId, state: 'idle', message: '', title });
    } else {
      await postJson('/state', { conversationId, state: 'ready', message: '任务完成!', title });
    }
  }

  process.stdout.write('{}');
}

main().catch(() => {
  process.stdout.write('{}');
});
