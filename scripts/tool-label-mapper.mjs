import path from 'node:path';

/**
 * 将字符串压缩为单行并在超出长度时截断。
 */
export function truncateInline(text, maxLen = 16) {
  const clean = String(text ?? '')
    .replace(/[\r\n\t]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  if (!clean) return '';
  if (clean.length <= maxLen) return clean;
  return clean.slice(0, Math.max(1, maxLen));
}

function extractFileName(args) {
  if (!args || typeof args !== 'object') return '';
  const raw =
    args.Path ??
    args.path ??
    args.TargetFile ??
    args.targetFile ??
    args.FilePath ??
    args.filePath ??
    args.file ??
    '';
  const str = String(raw || '').trim();
  if (!str) return '';
  const normalized = str.replace(/\\/g, '/');
  const base = path.posix.basename(normalized);
  return truncateInline(base || str, 12);
}

function extractCommandSummary(args) {
  if (!args || typeof args !== 'object') return null;
  const raw = String(args.CommandLine ?? args.command ?? args.cmd ?? '').trim();
  if (!raw) return null;
  const lower = raw.toLowerCase();

  if (/\b(vitest|jest|pytest|mocha|ava|xunit|nunit|ctest)\b|\b(npm|pnpm|yarn|bun)\s+(run\s+)?test\b|\bdotnet\s+test\b|\bgo\s+test\b|\bcargo\s+test\b/.test(lower)) {
    return { zh: '正在跑测试', en: 'Running tests' };
  }
  if (/\b(tsc|csc|msbuild|webpack|vite\s+build|esbuild|rollup)\b|\b(npm|pnpm|yarn|bun)\s+(run\s+)?build\b|\bdotnet\s+build\b|\bgo\s+build\b|\bcargo\s+build\b|\bbuild\.ps1\b|\bbuild\.sh\b/.test(lower)) {
    return { zh: '正在构建项目', en: 'Building project' };
  }
  if (/\b(eslint|prettier|stylelint|ruff|flake8|mypy|rubocop)\b|\b(npm|pnpm|yarn|bun)\s+(run\s+)?lint\b/.test(lower)) {
    return { zh: '正在跑检查', en: 'Running lint' };
  }
  if (/^\s*git\b/.test(lower)) {
    return { zh: '正在执行 Git', en: 'Running Git' };
  }
  if (/\b(npm|pnpm|yarn|bun)\s+(install|add|i)\b|\bpip\s+install\b/.test(lower)) {
    return { zh: '正在安装依赖', en: 'Installing deps' };
  }

  const firstToken = truncateInline(raw.split(/\s+/)[0].replace(/^["']|["']$/g, ''), 10);
  if (!firstToken) return { zh: '正在执行命令', en: 'Running command' };
  return {
    zh: `正在执行 ${firstToken}`,
    en: `Running ${firstToken}`
  };
}

function extractSearchTerm(args) {
  if (!args || typeof args !== 'object') return '';
  const raw = args.Query ?? args.query ?? args.pattern ?? args.Pattern ?? args.SearchTerm ?? '';
  return truncateInline(raw, 10);
}

/**
 * 将原始工具调用（名称 + 参数）翻译为自然、简短的单行中英文状态文案。
 * 返回 { zh, en }，确保绝不暴露原始技术工具名。
 */
export function formatToolLabel(toolCall) {
  const name = String(toolCall?.name || '').trim();
  const lowerName = name.toLowerCase();
  const args = (toolCall && typeof toolCall.args === 'object' && toolCall.args) || {};

  if (lowerName === 'call_mcp_tool') {
    const server = String(args.ServerName || '').toLowerCase();
    const mcpTool = String(args.ToolName || '').toLowerCase();
    if (server.includes('windows-mcp')) {
      if (mcpTool === 'screenshot' || mcpTool === 'snapshot') {
        return { zh: '正在看屏幕', en: 'Viewing screen' };
      }
      if (mcpTool === 'app') {
        return { zh: '正在切换应用', en: 'Switching app' };
      }
      return { zh: '正在操作桌面', en: 'Controlling desktop' };
    }
    if (server.includes('agy-dart_fast')) {
      if (mcpTool === 'apply_edits') return { zh: '正在改代码', en: 'Editing code' };
      if (mcpTool === 'batch_search') return { zh: '正在搜代码', en: 'Searching code' };
      if (mcpTool === 'run_checks') return { zh: '正在跑检查', en: 'Running checks' };
      if (mcpTool === 'read_slices') return { zh: '正在看代码结构', en: 'Scanning code' };
      return { zh: '正在分析代码', en: 'Analyzing code' };
    }
    if (server.includes('agy-dart_context')) {
      if (mcpTool === 'ctx_search') return { zh: '正在检索上下文', en: 'Searching context' };
      return { zh: '正在分析长文本', en: 'Analyzing context' };
    }
    if (server.includes('antigravity-pet')) {
      return { zh: '正在更新桌宠', en: 'Updating pet' };
    }
    return { zh: '正在调用扩展工具', en: 'Running tool' };
  }

  if (lowerName === 'read_file' || lowerName === 'view_file' || lowerName === 'view_file_outline' || lowerName === 'view_code_item') {
    const file = extractFileName(args);
    return file
      ? { zh: `正在读 ${file}`, en: `Reading ${file}` }
      : { zh: '正在读文件', en: 'Reading file' };
  }

  if (lowerName === 'write_file' || lowerName === 'write_to_file') {
    const file = extractFileName(args);
    return file
      ? { zh: `正在写 ${file}`, en: `Writing ${file}` }
      : { zh: '正在写文件', en: 'Writing file' };
  }

  if (lowerName === 'edit_file' || lowerName === 'replace_file_content' || lowerName === 'multi_replace_file_content') {
    const file = extractFileName(args);
    return file
      ? { zh: `正在改 ${file}`, en: `Editing ${file}` }
      : { zh: '正在改代码', en: 'Editing code' };
  }

  if (lowerName === 'run_command') {
    return extractCommandSummary(args) || { zh: '正在执行命令', en: 'Running command' };
  }

  if (lowerName === 'grep' || lowerName === 'grep_search' || lowerName === 'ripgrep') {
    const term = extractSearchTerm(args);
    return term
      ? { zh: `正在搜 ${term}`, en: `Searching ${term}` }
      : { zh: '正在搜代码', en: 'Searching code' };
  }

  if (lowerName === 'search' || lowerName === 'find_by_name' || lowerName === 'list_dir') {
    return { zh: '正在找文件', en: 'Finding files' };
  }

  if (lowerName === 'search_web' || lowerName === 'read_url_content') {
    return { zh: '正在查资料', en: 'Searching web' };
  }

  if (lowerName === 'invoke_subagent') {
    return { zh: '正在调度子代理', en: 'Running subagent' };
  }

  if (lowerName === 'ask_question') {
    return { zh: '等待你回答', en: 'Waiting for answer' };
  }

  if (lowerName === 'manage_task') {
    return { zh: '正在管理后台任务', en: 'Managing tasks' };
  }

  if (lowerName === 'exit_plan_mode') {
    return { zh: '准备开始执行', en: 'Starting execution' };
  }

  return { zh: '正在执行工具', en: 'Running tool' };
}
