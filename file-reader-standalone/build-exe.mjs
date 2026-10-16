import { spawnSync } from 'node:child_process';
import { copyFile, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const buildDir = join(here, 'build');
const distDir = join(here, 'dist');
const entryPath = join(buildDir, 'sea-boot.cjs');
const blobPath = join(buildDir, 'sea-prep.blob');
const configPath = join(buildDir, 'sea-config.json');
const exePath = join(distDir, 'File Reader.exe');

const codec = await readFile(join(here, 'codec.mjs'), 'utf8');
const server = await readFile(join(here, 'server.mjs'), 'utf8');
const moduleSource = `${codec.replace(/^export /gm, '')}\n${server.replace(
  "import { encodeJson, parseJson } from './codec.mjs';\n",
  '',
)}`;
const entry = `const source = ${JSON.stringify(moduleSource)};
import('data:text/javascript;charset=utf-8,' + encodeURIComponent(source)).catch((error) => {
  console.error(error);
  process.exit(1);
});
`;

await rm(buildDir, { recursive: true, force: true });
await mkdir(buildDir, { recursive: true });
await mkdir(distDir, { recursive: true });
await writeFile(entryPath, entry);
await writeFile(
  configPath,
  JSON.stringify(
    {
      main: 'sea-boot.cjs',
      output: 'sea-prep.blob',
      disableExperimentalSEAWarning: true,
      assets: {
        'index.html': join(here, 'public', 'index.html'),
        'app.js': join(here, 'public', 'app.js'),
        'styles.css': join(here, 'public', 'styles.css'),
      },
    },
    null,
    2,
  ),
);

const sea = spawnSync(process.execPath, ['--experimental-sea-config', configPath], {
  cwd: buildDir,
  stdio: 'inherit',
});
if (sea.status !== 0) {
  process.exit(sea.status ?? 1);
}

await copyFile(process.execPath, exePath);
const postject = spawnSync(
  process.execPath,
  [
    join(here, 'node_modules', 'postject', 'dist', 'cli.js'),
    exePath,
    'NODE_SEA_BLOB',
    blobPath,
    '--sentinel-fuse',
    'NODE_SEA_FUSE_fce680ab2cc467b6e072b8b5df1996b2',
    '--overwrite',
  ],
  { stdio: 'inherit' },
);
if (postject.status !== 0) {
  process.exit(postject.status ?? 1);
}
console.log(exePath);
