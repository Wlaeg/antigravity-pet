#!/usr/bin/env node
/**
 * Aggregate global stars (from linked GitHub Issue reactions) and downloads
 * (from GitHub Releases assets matching <pet-id>.zip) into community-stats.json.
 */
import fs from 'fs';
import path from 'path';
import https from 'https';

const repoRoot = process.cwd();
const manifestPath = path.join(repoRoot, 'community-pets.json');
const statsPath = path.join(repoRoot, 'community-stats.json');
const repoSlug = process.env.GITHUB_REPOSITORY || 'Wlaeg/antigravity-pet';
const token = process.env.GITHUB_TOKEN || '';

function ghGet(apiPath) {
  return new Promise((resolve, reject) => {
    const headers = {
      'User-Agent': 'AntigravityPet-Stats-Bot',
      'Accept': 'application/vnd.github+json'
    };
    if (token) headers.Authorization = `Bearer ${token}`;
    https.get({ hostname: 'api.github.com', path: apiPath, headers, timeout: 15000 }, (res) => {
      let d = '';
      res.on('data', (c) => (d += c));
      res.on('end', () => {
        if (res.statusCode >= 400) {
          return reject(new Error(`GET ${apiPath} -> HTTP ${res.statusCode}: ${d}`));
        }
        resolve(d ? JSON.parse(d) : {});
      });
    }).on('error', reject);
  });
}

async function main() {
  if (!fs.existsSync(manifestPath)) {
    throw new Error('Missing community-pets.json');
  }
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  const prevStats = fs.existsSync(statsPath)
    ? JSON.parse(fs.readFileSync(statsPath, 'utf8'))
    : { version: 1, pets: {} };
  const prevPets = prevStats.pets || {};

  // Collect download_count from GitHub Releases assets matching <id>.zip
  const releaseDownloads = {};
  try {
    const releases = await ghGet(`/repos/${repoSlug}/releases?per_page=30`);
    if (Array.isArray(releases)) {
      for (const rel of releases) {
        for (const asset of (rel.assets || [])) {
          const name = String(asset.name || '').toLowerCase();
          if (name.endsWith('.zip')) {
            const id = name.replace(/\.zip$/, '');
            releaseDownloads[id] = (releaseDownloads[id] || 0) + Number(asset.download_count || 0);
          }
        }
      }
    }
  } catch (err) {
    console.warn('Release download fetch skipped:', err.message);
  }

  // Collect live global downloads from Abacus counter (zero-release, cross-machine)
  const liveDownloads = {};
  await Promise.all((manifest.pets || []).map(async (pet) => {
    const id = String(pet.id || '').trim().toLowerCase().replace(/[^a-z0-9-_]/g, '-');
    if (!id) return;
    try {
      const val = await new Promise((resolve) => {
        https.get(`https://abacus.jasoncameron.dev/get/wlaeg-antigravity-pet/dl-${id}`, {
          headers: { 'User-Agent': 'AntigravityPet-Stats-Bot' },
          timeout: 5000
        }, (res) => {
          let d = '';
          res.on('data', (c) => (d += c));
          res.on('end', () => {
            try {
              const j = JSON.parse(d || '{}');
              resolve(typeof j.value === 'number' ? j.value : 0);
            } catch {
              resolve(0);
            }
          });
        }).on('error', () => resolve(0));
      });
      liveDownloads[id] = val;
    } catch {}
  }));

  const nextPets = {};
  for (const pet of (manifest.pets || [])) {
    const id = String(pet.id || '').trim().toLowerCase();
    if (!id) continue;
    const prev = prevPets[id] || {};
    let stars = Number(prev.stars || 0);

    const ghUrl = String(pet.githubUrl || '');
    const m = ghUrl.match(/\/issues\/(\d+)/);
    if (m) {
      try {
        const issueData = await ghGet(`/repos/${repoSlug}/issues/${m[1]}`);
        const rx = issueData.reactions || {};
        // Count positive reactions (+1, heart, hooray, rocket, eyes)
        const count = Number(rx['+1'] || 0) + Number(rx.heart || 0) + Number(rx.hooray || 0) + Number(rx.rocket || 0);
        stars = Math.max(0, count);
      } catch (err) {
        console.warn(`Issue reactions fetch skipped for ${id}:`, err.message);
      }
    }

    const downloads = Math.max(
      Number(prev.downloads || 0),
      Number(releaseDownloads[id] || 0),
      Number(liveDownloads[id] || 0)
    );
    nextPets[id] = { stars, downloads };
  }

  const out = {
    version: 1,
    updatedAt: new Date().toISOString(),
    pets: nextPets
  };
  fs.writeFileSync(statsPath, JSON.stringify(out, null, 2) + '\n', 'utf8');
  console.log(JSON.stringify(out, null, 2));
}

main().catch((err) => {
  console.error(err.message || String(err));
  process.exit(1);
});
