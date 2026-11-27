#!/usr/bin/env node
/**
 * Parse a GitHub Issue Form body for community pet submission,
 * download & validate the .zip archive, save it to community/packs/<id>.zip,
 * and upsert the entry in community-pets.json.
 */
import fs from 'fs';
import path from 'path';
import https from 'https';
import { execFileSync } from 'child_process';

const issueBody = process.env.ISSUE_BODY || '';
const issueAuthor = process.env.ISSUE_AUTHOR || 'Community';
const issueUrl = process.env.ISSUE_URL || '';
const repoRoot = process.cwd();

function parseIssueForm(body) {
  const sections = {};
  const parts = body.split(/^###\s+/m).slice(1);
  for (const part of parts) {
    const nl = part.indexOf('\n');
    if (nl < 0) continue;
    const heading = part.slice(0, nl).trim();
    const val = part.slice(nl + 1).trim();
    sections[heading] = val === '_No response_' ? '' : val;
  }
  return sections;
}

function pick(sections, prefix) {
  const key = Object.keys(sections).find((k) => k.toLowerCase().startsWith(prefix.toLowerCase()));
  return key ? sections[key].trim() : '';
}

function extractUrlFromField(raw) {
  const mdLink = raw.match(/!?\[[^\]]*\]\((https:\/\/[^)\s]+)\)/i);
  if (mdLink) return mdLink[1];
  const imgTag = raw.match(/<img[^>]+src=["'](https:\/\/[^"'\s]+)["']/i);
  if (imgTag) return imgTag[1];
  const bare = raw.match(/(https:\/\/[^\s<>")]+)/i);
  return bare ? bare[1] : '';
}

function detectImageExt(buf) {
  if (!buf || buf.length < 12) return null;
  if (buf[0] === 0x89 && buf[1] === 0x50 && buf[2] === 0x4e && buf[3] === 0x47) return 'png';
  if (buf[0] === 0xff && buf[1] === 0xd8 && buf[2] === 0xff) return 'jpg';
  if (buf.slice(0, 4).toString('ascii') === 'RIFF' && buf.slice(8, 12).toString('ascii') === 'WEBP') return 'webp';
  return null;
}

function downloadFile(url, maxBytes = 20 * 1024 * 1024, redirects = 5) {
  return new Promise((resolve, reject) => {
    const parsed = new URL(url);
    if (parsed.protocol !== 'https:') return reject(new Error('Only HTTPS URLs are allowed'));
    https.get(parsed, { headers: { 'User-Agent': 'AntigravityPet-Approve-Bot' }, timeout: 20000 }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location && redirects > 0) {
        res.resume();
        return downloadFile(new URL(res.headers.location, parsed).toString(), maxBytes, redirects - 1).then(resolve, reject);
      }
      if (res.statusCode < 200 || res.statusCode >= 300) {
        res.resume();
        return reject(new Error(`Download failed with HTTP ${res.statusCode}`));
      }
      const chunks = [];
      let total = 0;
      res.on('data', (c) => {
        total += c.length;
        if (total > maxBytes) return reject(new Error('Archive exceeds 20 MB limit'));
        chunks.push(c);
      });
      res.on('end', () => resolve(Buffer.concat(chunks)));
      res.on('error', reject);
    }).on('error', reject);
  });
}

async function main() {
  const sections = parseIssueForm(issueBody);
  const rawId = pick(sections, 'Pet ID');
  const displayName = pick(sections, 'Display Name');
  const description = pick(sections, 'Description');
  const themeColorRaw = pick(sections, 'Theme Color');
  const tagsRaw = pick(sections, 'Tags');
  const previewField = pick(sections, 'Pet Full-Body Preview Image');
  const zipField = pick(sections, 'Pet Pack .zip');
  const previewInputUrl = extractUrlFromField(previewField);
  const zipUrl = extractUrlFromField(zipField);

  if (!zipUrl) {
    throw new Error('Could not find a valid HTTPS .zip URL or uploaded attachment in the issue body.');
  }
  if (!previewInputUrl) {
    throw new Error('Please provide a full-body preview image (PNG/WebP/JPG) in the issue body.');
  }

  const tmpZip = path.join(repoRoot, '.tmp-submission.zip');
  try {
    const buf = await downloadFile(zipUrl);
    fs.writeFileSync(tmpZip, buf);

    const out = execFileSync(process.execPath, [path.join(repoRoot, 'scripts/validate-pet-pack.mjs'), tmpZip], {
      encoding: 'utf8'
    });
    const validated = JSON.parse(out);
    const id = (rawId || validated.id).trim().toLowerCase().replace(/[^a-z0-9-_]/g, '-');
    if (id !== validated.id) {
      throw new Error(`Issue Pet ID "${id}" does not match pet.json id "${validated.id}".`);
    }

    const packsDir = path.join(repoRoot, 'community', 'packs');
    const previewsDir = path.join(repoRoot, 'community', 'previews');
    fs.mkdirSync(packsDir, { recursive: true });
    fs.mkdirSync(previewsDir, { recursive: true });
    const destZipRel = `community/packs/${id}.zip`;
    fs.copyFileSync(tmpZip, path.join(repoRoot, destZipRel));

    const prevBuf = await downloadFile(previewInputUrl, 5 * 1024 * 1024);
    const prevExt = detectImageExt(prevBuf);
    if (!prevExt) {
      throw new Error('Preview image must be a valid PNG, WebP, or JPG file.');
    }
    const destPreviewRel = `community/previews/${id}.${prevExt}`;
    fs.writeFileSync(path.join(repoRoot, destPreviewRel), prevBuf);

    const manifestPath = path.join(repoRoot, 'community-pets.json');
    const manifest = fs.existsSync(manifestPath)
      ? JSON.parse(fs.readFileSync(manifestPath, 'utf8'))
      : { version: 1, pets: [] };
    if (!Array.isArray(manifest.pets)) manifest.pets = [];

    const tags = tagsRaw
      ? tagsRaw.split(/[,，]/).map((s) => s.trim().toLowerCase()).filter(Boolean)
      : [];
    if (validated.capabilities && validated.capabilities.hasWorkingAnim && !tags.includes('working-anim')) {
      tags.push('working-anim');
    }

    const themeColor = /^#[0-9a-fA-F]{6}$/.test(themeColorRaw)
      ? themeColorRaw
      : (validated.themeColor || '#0EA5E9');

    const entry = {
      id,
      displayName: displayName || validated.displayName,
      description: description || validated.description || '',
      author: issueAuthor,
      themeColor,
      version: '1.0.0',
      ...(tags.length ? { tags } : {}),
      hasWorkingAnim: Boolean(validated.capabilities && validated.capabilities.hasWorkingAnim),
      updatedAt: new Date().toISOString().slice(0, 10),
      localZip: destZipRel,
      zipUrl: `https://raw.githubusercontent.com/Wlaeg/antigravity-pet/main/${destZipRel}`,
      localPreview: destPreviewRel,
      previewUrl: `https://raw.githubusercontent.com/Wlaeg/antigravity-pet/main/${destPreviewRel}`,
      ...(issueUrl ? { githubUrl: issueUrl } : {})
    };

    const idx = manifest.pets.findIndex((p) => String(p.id || '').toLowerCase() === id);
    if (idx >= 0) {
      const prevVer = String(manifest.pets[idx].version || '1.0.0').split('.').map(Number);
      if (prevVer.length === 3 && prevVer.every(Number.isFinite)) {
        entry.version = `${prevVer[0]}.${prevVer[1]}.${prevVer[2] + 1}`;
      }
      manifest.pets[idx] = entry;
    } else {
      manifest.pets.push(entry);
    }

    fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n', 'utf8');
    console.log(JSON.stringify({ ok: true, id, displayName: entry.displayName, version: entry.version }));
  } finally {
    try { fs.rmSync(tmpZip, { force: true }); } catch {}
  }
}

main().catch((err) => {
  console.error(err.message || String(err));
  process.exit(1);
});
