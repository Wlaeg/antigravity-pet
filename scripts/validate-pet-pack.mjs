#!/usr/bin/env node
/**
 * Validate a community pet pack (.zip or extracted directory) against CUSTOM_PET_SPEC (v3).
 * Usage: node scripts/validate-pet-pack.mjs <path-to-zip-or-dir>
 */
import fs from 'fs';
import path from 'path';
import os from 'os';
import { execFileSync } from 'child_process';

function findPetManifestDir(rootDir, depth = 0) {
  if (depth > 3 || !fs.existsSync(rootDir)) return null;
  if (fs.existsSync(path.join(rootDir, 'pet.json'))) return rootDir;
  for (const ent of fs.readdirSync(rootDir, { withFileTypes: true })) {
    if (ent.isDirectory() && !ent.name.startsWith('.') && ent.name !== '__MACOSX') {
      const hit = findPetManifestDir(path.join(rootDir, ent.name), depth + 1);
      if (hit) return hit;
    }
  }
  return null;
}

function extractZipCrossPlatform(zipPath, destDir) {
  fs.mkdirSync(destDir, { recursive: true });
  if (process.platform === 'win32') {
    execFileSync(
      'powershell',
      ['-NoProfile', '-Command', `Expand-Archive -LiteralPath '${zipPath.replace(/'/g, "''")}' -DestinationPath '${destDir.replace(/'/g, "''")}' -Force`],
      { stdio: 'pipe' }
    );
  } else {
    execFileSync('unzip', ['-q', '-o', zipPath, '-d', destDir], { stdio: 'pipe' });
  }
}

function fail(msg) {
  console.error(JSON.stringify({ ok: false, error: msg }));
  process.exit(1);
}

const target = process.argv[2];
if (!target) fail('Missing target path (.zip or directory)');
const absTarget = path.resolve(target);
if (!fs.existsSync(absTarget)) fail(`Path not found: ${absTarget}`);

let tmpRoot = null;
try {
  let workDir = absTarget;
  if (fs.statSync(absTarget).isFile()) {
    if (!absTarget.toLowerCase().endsWith('.zip')) fail('File must be a .zip archive');
    const stat = fs.statSync(absTarget);
    if (stat.size === 0 || stat.size > 20 * 1024 * 1024) {
      fail('Archive size must be between 1 byte and 20 MB');
    }
    tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'pet-validate-'));
    extractZipCrossPlatform(absTarget, tmpRoot);
    workDir = tmpRoot;
  }

  const manifestDir = findPetManifestDir(workDir);
  if (!manifestDir) fail('Missing pet.json manifest inside pet pack');

  const metaRaw = fs.readFileSync(path.join(manifestDir, 'pet.json'), 'utf8');
  let meta;
  try {
    meta = JSON.parse(metaRaw);
  } catch (e) {
    fail(`Invalid pet.json JSON: ${e.message}`);
  }

  const id = String(meta.id || '').trim().toLowerCase();
  if (!/^[a-z0-9][a-z0-9-_]{1,48}$/.test(id)) {
    fail('pet.json "id" must match /^[a-z0-9][a-z0-9-_]{1,48}$/');
  }

  const displayName = String(meta.displayName || '').trim();
  if (!displayName) fail('pet.json "displayName" is required');

  const sheetRel = String(meta.spritesheetPath || 'spritesheet.png');
  const hasSheet = fs.existsSync(path.join(manifestDir, sheetRel));
  const hasIdleDir = fs.existsSync(path.join(manifestDir, 'idle'));
  const hasRunningDir = fs.existsSync(path.join(manifestDir, 'running'));
  const singleCandidates = [
    meta.idleImage,
    meta.image,
    'idle.png',
    'idle.webp',
    'pet.png',
    'pet.webp',
    'avatar.png',
    'avatar.webp'
  ].filter(Boolean);
  const hasSingleDeclared = singleCandidates.some((rel) => fs.existsSync(path.join(manifestDir, String(rel))));
  const hasRootImage = fs.readdirSync(manifestDir).some((f) => /\.(png|webp|jpg|jpeg)$/i.test(f));

  if (!hasSheet && !hasIdleDir && !hasSingleDeclared && !hasRootImage) {
    fail('Missing sprite assets: provide spritesheet.png, idle/ frames, or a base image (e.g. idle.png)');
  }

  if (meta.themeColor && !/^#[0-9a-fA-F]{6}$/.test(String(meta.themeColor))) {
    fail('pet.json "themeColor" must be a 6-digit hex color like #0EA5E9');
  }

  const result = {
    ok: true,
    id,
    displayName,
    description: meta.description || '',
    themeColor: meta.themeColor || '#0EA5E9',
    spriteVersionNumber: Number(meta.spriteVersionNumber || 3),
    capabilities: {
      hasSpritesheet: hasSheet,
      hasIdleFrames: hasIdleDir,
      hasWorkingAnim: hasSheet || hasRunningDir,
      singleImageFallback: !hasSheet && !hasIdleDir && (hasSingleDeclared || hasRootImage)
    }
  };
  console.log(JSON.stringify(result, null, 2));
} finally {
  if (tmpRoot) {
    try { fs.rmSync(tmpRoot, { recursive: true, force: true }); } catch {}
  }
}
