#!/usr/bin/env node
/**
 * Builds the dated panel deliverables:
 *   archive/file-reader-YYYYMMDD.ch5z
 *   archive/file-reader-YYYYMMDD.chd
 *
 * The datecode is the local calendar date. The working contract in contracts/
 * stays undated so the project can rebuild it; these two files are the compile result.
 */
import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const now = new Date();
const datecode = [
  now.getFullYear(),
  String(now.getMonth() + 1).padStart(2, '0'),
  String(now.getDate()).padStart(2, '0'),
].join('');
const stem = `file-reader-${datecode}`;

const cliName = process.platform === 'win32' ? 'ch5-cli.cmd' : 'ch5-cli';
const cli = join(root, 'node_modules', '.bin', cliName);
const command = `"${cli}" archive -p ${stem} -d dist -o archive -c contracts/file-reader.cse2j`;
const result = spawnSync(command, { cwd: root, stdio: 'inherit', shell: true });
if ((result.status ?? 1) !== 0) {
  process.exit(result.status ?? 1);
}

const chd = join(root, 'contracts', 'file-reader.chd');
if (!existsSync(chd)) {
  console.error(`Missing ${chd}. Run npm run generate:contract first.`);
  process.exit(1);
}

mkdirSync(join(root, 'archive'), { recursive: true });
copyFileSync(chd, join(root, 'archive', `${stem}.chd`));
console.log(`Wrote archive/${stem}.chd`);
console.log(`Wrote archive/${stem}.ch5z`);
