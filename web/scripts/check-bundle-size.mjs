import { gzipSync } from 'node:zlib';
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

/**
 * Budget de bundle : 200 Ko gzip, imposé par docs/07-frontend.md.
 * La cible est un iPad 5 (A9, 2 Go de RAM) : ce budget est une contrainte
 * de produit, pas une préférence de style.
 */
const BUDGET_BYTES = 200 * 1024;
const DIST = new URL('../../src/RoomOS.Core/wwwroot/assets/', import.meta.url).pathname;

const files = await readdir(DIST);
const assets = files.filter((f) => f.endsWith('.js') || f.endsWith('.css'));

let total = 0;
const rows = [];

for (const file of assets) {
  const raw = await readFile(join(DIST, file));
  const gzipped = gzipSync(raw).byteLength;
  total += gzipped;
  rows.push({ file, gzip: gzipped });
}

rows.sort((a, b) => b.gzip - a.gzip);

for (const { file, gzip } of rows) {
  console.log(`  ${(gzip / 1024).toFixed(1).padStart(7)} Ko  ${file}`);
}

const pct = ((total / BUDGET_BYTES) * 100).toFixed(0);
console.log(`\n  total ${(total / 1024).toFixed(1)} Ko gzip / budget ${BUDGET_BYTES / 1024} Ko (${pct} %)`);

if (total > BUDGET_BYTES) {
  console.error(`\nBudget dépassé de ${((total - BUDGET_BYTES) / 1024).toFixed(1)} Ko.`);
  process.exit(1);
}
