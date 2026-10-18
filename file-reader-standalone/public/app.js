const SLOT_COUNT = 64;
const LOCATIONS = [
  { code: 1, path: '/nvram/' },
  { code: 2, path: '/user/' },
  { code: 3, path: '/html/' },
  { code: 4, path: '/rm/' },
  { code: 5, path: '/rm2/' },
];
const EXTENSIONS = { 1: '.txt', 2: '.csv', 3: '.xml', 4: '.json', 5: '.ini', 6: '.bin' };
const FORMAT_NAMES = { 1: 'Text', 2: 'CSV', 3: 'XML', 4: 'JSON', 5: 'INI', 6: 'Binary' };

function blankSlots() {
  return {
    digitals: Array.from({ length: SLOT_COUNT }, () => 0),
    analogs: Array.from({ length: SLOT_COUNT }, () => 0),
    serials: Array.from({ length: SLOT_COUNT }, () => ''),
  };
}

const edits = blankSlots();
const stored = blankSlots();
const rows = document.querySelector('#rows');
const statusLine = document.querySelector('#status-line');
let kind = 'digital';
let locationCode = 0;
let formatCode = 4;
let busy = false;
let exists = null;
let backupExists = false;
let encrypted = false;
let sizeText = '';
let detected = 0;
let confirmAction = null;

function must(selector) {
  const element = document.querySelector(selector);
  if (!element) {
    throw new Error(`Missing ${selector}`);
  }
  return element;
}

function setStatus(text) {
  statusLine.textContent = text;
  statusLine.title = text;
}

function clampAnalog(value) {
  if (!Number.isFinite(value)) {
    return 0;
  }
  return Math.max(0, Math.min(65535, Math.round(value)));
}

function folderPath() {
  const forced = LOCATIONS.find((item) => item.code === locationCode);
  if (forced) {
    return forced.path;
  }
  return must('#location-parameter').value;
}

function activeFormat() {
  return formatCode >= 1 && formatCode <= 6
    ? formatCode
    : Number(must('#format-parameter').value);
}

function baseName() {
  const override = must('#name-override').value.trim();
  return (override || must('#filename').value.trim() || 'config').replace(/[\\/]/g, '');
}

function extension() {
  return EXTENSIONS[activeFormat()] || '.json';
}

function remotePath(suffix = '') {
  return `${folderPath()}${baseName()}${extension()}${suffix}`.replace(/\/{2,}/g, '/');
}

function remoteUrl(suffix = '') {
  const host = must('#host').value.trim();
  const scheme = must('#scheme').value;
  return `${scheme}://${host}${remotePath(suffix)}`;
}

function job(op, text, suffix = '') {
  return {
    op,
    url: remoteUrl(suffix),
    user: must('#user').value,
    password: must('#password').value,
    insecure: must('#insecure').checked,
    text,
  };
}

async function post(path, body) {
  const response = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const payload = await response.json();
  if (!response.ok) {
    throw new Error(payload.error || 'Request failed');
  }
  return payload;
}

function copySlots(from, to) {
  for (let index = 0; index < SLOT_COUNT; index += 1) {
    to.digitals[index] = from.digitals[index] ? 1 : 0;
    to.analogs[index] = clampAnalog(Number(from.analogs[index] ?? 0));
    to.serials[index] = String(from.serials[index] ?? '').slice(0, 256);
  }
}

function paintLocation() {
  document.querySelectorAll('[data-location]').forEach((button) => {
    button.setAttribute('aria-pressed', String(Number(button.dataset.location) === locationCode));
  });
}

function paintFormat() {
  document.querySelectorAll('[data-format]').forEach((button) => {
    button.setAttribute('aria-pressed', String(Number(button.dataset.format) === formatCode));
  });
  must('#detected-line').textContent = detected
    ? `Detected: ${FORMAT_NAMES[detected] || detected}`
    : 'Detected: Not read';
  document.querySelectorAll('[data-detected]').forEach((dot) => {
    dot.dataset.on = String(Number(dot.dataset.detected) === detected);
  });
}

function paintToggles() {
  must('#match-partial').setAttribute('aria-pressed', String(must('#match-partial').dataset.on === 'true'));
  for (const id of ['auto-create', 'enable-backup', 'enable-history', 'encrypt']) {
    const button = must(`#${id}`);
    button.setAttribute('aria-pressed', String(button.dataset.on === 'true'));
  }
}

function differs(index) {
  if (kind === 'digital') {
    return edits.digitals[index] !== stored.digitals[index];
  }
  if (kind === 'analog') {
    return edits.analogs[index] !== stored.analogs[index];
  }
  return edits.serials[index] !== stored.serials[index];
}

function paintRow(index) {
  const row = rows.querySelector(`.row[data-kind="${kind}"][data-slot="${index + 1}"]`);
  if (!row) {
    return;
  }
  const diff = differs(index);
  row.dataset.diff = String(diff);
  row.hidden = must('#diff-only').checked && !diff;
  if (kind === 'digital') {
    const button = row.querySelector('button');
    button.textContent = edits.digitals[index] ? 'On' : 'Off';
    button.setAttribute('aria-pressed', String(edits.digitals[index] === 1));
    row.querySelector('.file-value').textContent = stored.digitals[index] ? 'On' : 'Off';
  } else if (kind === 'analog') {
    const input = row.querySelector('input');
    if (document.activeElement !== input) {
      input.value = String(edits.analogs[index]);
    }
    row.querySelector('.file-value').textContent = String(stored.analogs[index]);
  } else {
    const input = row.querySelector('input');
    if (document.activeElement !== input) {
      input.value = edits.serials[index];
    }
    const file = stored.serials[index];
    const fileValue = row.querySelector('.file-value');
    fileValue.textContent = file || 'empty';
    fileValue.dataset.empty = String(file.length === 0);
  }
}

function paintRows() {
  rows.dataset.kind = kind;
  rows.dataset.diffOnly = String(must('#diff-only').checked);
  let visible = 0;
  for (let index = 0; index < SLOT_COUNT; index += 1) {
    paintRow(index);
    const row = rows.querySelector(`.row[data-kind="${kind}"][data-slot="${index + 1}"]`);
    if (row && !row.hidden) {
      visible += 1;
    }
  }
  must('#no-diffs').hidden = !(must('#diff-only').checked && visible === 0);
}

function paintChrome() {
  const path = remotePath();
  must('#file-name').textContent = `${baseName()}${extension()}`;
  must('#file-name').title = path;
  must('#cip-label').textContent = must('#host').value.trim() || 'No host';
  must('#cip-dot').dataset.state = busy ? 'connecting' : 'online';
  must('#chip-exists').textContent = exists === true ? 'Exists' : exists === false ? 'Missing' : 'File';
  must('#chip-exists').dataset.state = exists === true ? 'yes' : exists === false ? 'no' : 'unknown';
  must('#chip-encrypted').textContent = encrypted ? 'Encrypted' : 'Plain';
  must('#chip-encrypted').dataset.state = encrypted ? 'yes' : 'unknown';
  must('#encrypted-state').textContent = encrypted ? 'File header says encrypted' : 'Not encrypted';
  must('#chip-backup').textContent = backupExists ? 'Backup' : 'No backup';
  must('#chip-backup').dataset.state = backupExists ? 'yes' : 'unknown';
  must('#backup-state').textContent = backupExists ? 'Backup is on disk' : 'No backup';
  must('#chip-busy').textContent = busy ? 'Busy' : 'Idle';
  must('#chip-busy').dataset.state = busy ? 'busy' : 'unknown';
  must('#size-fb').textContent = sizeText || 'Size —';
  const open = !busy;
  must('#read').disabled = !open;
  must('#write').disabled = !open;
  must('#delete').disabled = !open;
  must('#restore').disabled = !open;
  must('#delete-backup').disabled = !open;
  must('#pull').textContent = `Publish serves this JSON for Remote File Sync. Local_Path ${remotePath()}. Retrieve writes it on the processor.`;
}

function applyParsed(next) {
  copySlots(next, stored);
  detected = 4;
  encrypted = false;
  paintRows();
  paintFormat();
}

function useFileValues() {
  copySlots(stored, edits);
  paintRows();
}

function clearEdits() {
  copySlots(blankSlots(), edits);
  paintRows();
}

async function transfer(op, text, suffix = '') {
  return post('/api/transfer', job(op, text, suffix));
}

async function refreshBackup() {
  try {
    const result = await transfer('check', undefined, '.bak');
    backupExists = result.exists === true;
  } catch {
    backupExists = false;
  }
}

async function resolveName() {
  if (must('#match-partial').dataset.on !== 'true') {
    return;
  }
  if (must('#scheme').value !== 'ftp') {
    setStatus('Match partial lists the folder over FTP. The exact name is used for http and https.');
    return;
  }
  try {
    const listed = await post('/api/transfer', {
      op: 'list',
      url: `${must('#scheme').value}://${must('#host').value.trim()}${folderPath()}`,
      user: must('#user').value,
      password: must('#password').value,
      insecure: must('#insecure').checked,
    });
    const fragment = baseName().toLowerCase();
    const ext = extension().toLowerCase();
    const match = listed.names
      ?.map((name) => name.split(/[\\/]/).pop())
      .filter((name) => name && name.toLowerCase().endsWith(ext) && name.toLowerCase().includes(fragment))
      .sort((left, right) => left.localeCompare(right))[0];
    if (match) {
      must('#name-override').value = match.slice(0, -ext.length);
    }
  } catch {
    setStatus('Could not list the folder. The exact name is used.');
  }
}

async function readFile() {
  await resolveName();
  const result = await transfer('retrieve');
  if (!result.exists) {
    exists = false;
    sizeText = '';
    detected = 0;
    if (must('#auto-create').dataset.on === 'true') {
      await writeFile();
      setStatus(`Missing. Wrote the current edits to ${remotePath()}.`);
      return;
    }
    setStatus(result.message);
    return;
  }
  exists = true;
  sizeText = `${new TextEncoder().encode(result.text).length} bytes`;
  encrypted = result.text.startsWith('ENC:AES:');
  try {
    const parsed = await post('/api/parse', { text: result.text });
    applyParsed(parsed.file);
    setStatus(`Read ${remotePath()}`);
  } catch (error) {
    detected = 0;
    setStatus(`${remotePath()} is not JSON signal data. ${error.message}`);
  }
  await refreshBackup();
}

async function encodeEdits() {
  return post('/api/encode', { file: edits });
}

async function keepBackup() {
  if (must('#enable-backup').dataset.on !== 'true') {
    return;
  }
  const current = await transfer('retrieve');
  if (!current.exists) {
    return;
  }
  if (must('#enable-history').dataset.on === 'true' && backupExists) {
    const previous = await transfer('retrieve', undefined, '.bak');
    if (previous.exists) {
      const stamp = new Date().toISOString().slice(0, 10).replace(/-/g, '');
      await transfer('overwrite', previous.text, `.bak.${stamp}`);
    }
  }
  await transfer('overwrite', current.text, '.bak');
}

async function writeFile() {
  if (must('#encrypt').dataset.on === 'true') {
    if (!must('#encryption-key').value) {
      throw new Error('A write with Encrypt on and no key is refused.');
    }
    throw new Error('Encrypt is applied by File Reader on the processor. Publish the JSON and write it there, or turn Encrypt off to store plain JSON.');
  }
  const encoded = await encodeEdits();
  await keepBackup();
  const result = await transfer('overwrite', encoded.text);
  copySlots(edits, stored);
  exists = true;
  encrypted = false;
  detected = activeFormat() === 4 ? 4 : 0;
  sizeText = `${new TextEncoder().encode(encoded.text).length} bytes`;
  await refreshBackup();
  paintRows();
  paintFormat();
  setStatus(`${result.message}: ${remotePath()}`);
}

function openConfirm(action) {
  confirmAction = action;
  must('#confirm-text').textContent = action === 'delete'
    ? 'Delete the file on the processor? The backup file is left in place.'
    : 'Delete the backup file?';
  must('#confirm').hidden = false;
}

async function runConfirmed() {
  const action = confirmAction;
  must('#confirm').hidden = true;
  confirmAction = null;
  busy = true;
  paintChrome();
  try {
    if (action === 'delete') {
      const result = await transfer('delete');
      exists = false;
      setStatus(`${result.message}: ${remotePath()}`);
    } else {
      const result = await transfer('delete', undefined, '.bak');
      backupExists = false;
      setStatus(`${result.message}: ${remotePath('.bak')}`);
    }
  } catch (error) {
    setStatus(error.message);
  } finally {
    busy = false;
    paintChrome();
  }
}

function tickClock() {
  const now = new Date();
  must('#clock-time').textContent = new Intl.DateTimeFormat(undefined, {
    hour: 'numeric',
    minute: '2-digit',
  }).format(now);
  must('#clock-date').textContent = new Intl.DateTimeFormat(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  }).format(now);
}

function buildRows() {
  for (const slotKind of ['digital', 'analog', 'serial']) {
    for (let index = 0; index < SLOT_COUNT; index += 1) {
      const row = document.createElement('div');
      row.className = 'row';
      row.dataset.kind = slotKind;
      row.dataset.slot = String(index + 1);
      row.dataset.diff = 'false';
      const label = document.createElement('span');
      label.className = 'row__index';
      label.textContent = String(index + 1);
      const fileValue = document.createElement('span');
      fileValue.className = 'file-value';
      if (slotKind === 'digital') {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'toggle';
        button.textContent = 'Off';
        button.addEventListener('click', () => {
          edits.digitals[index] = edits.digitals[index] ? 0 : 1;
          paintRow(index);
        });
        row.append(label, button, fileValue);
      } else if (slotKind === 'analog') {
        const input = document.createElement('input');
        input.type = 'number';
        input.min = '0';
        input.max = '65535';
        input.step = '1';
        input.value = '0';
        input.addEventListener('change', () => {
          edits.analogs[index] = clampAnalog(Number(input.value));
          input.value = String(edits.analogs[index]);
          paintRow(index);
        });
        row.append(label, input, fileValue);
      } else {
        const input = document.createElement('input');
        input.type = 'text';
        input.maxLength = 256;
        input.addEventListener('change', () => {
          edits.serials[index] = input.value.slice(0, 256);
          paintRow(index);
        });
        row.append(label, input, fileValue);
      }
      rows.append(row);
    }
  }
}

function bindToggle(id) {
  const button = must(`#${id}`);
  button.dataset.on = 'false';
  button.addEventListener('click', () => {
    button.dataset.on = String(button.dataset.on !== 'true');
    paintToggles();
    paintChrome();
  });
}

buildRows();
tickClock();
window.setInterval(tickClock, 5000);

document.querySelectorAll('[data-location]').forEach((button) => {
  button.addEventListener('click', () => {
    locationCode = Number(button.dataset.location);
    paintLocation();
    paintChrome();
  });
});
document.querySelectorAll('[data-format]').forEach((button) => {
  button.addEventListener('click', () => {
    formatCode = Number(button.dataset.format);
    must('#format-analog').value = String(formatCode);
    paintFormat();
    paintChrome();
  });
});
must('#format-analog').addEventListener('change', () => {
  const raw = must('#format-analog').value.trim();
  formatCode = raw === '' ? 0 : clampAnalog(Number(raw));
  must('#format-analog').value = raw === '' ? '' : String(formatCode);
  paintFormat();
  paintChrome();
});
must('#location-parameter').addEventListener('change', paintChrome);
must('#filename').addEventListener('change', paintChrome);
must('#name-override').addEventListener('change', paintChrome);
must('#host').addEventListener('change', paintChrome);
must('#format-parameter').addEventListener('change', () => {
  paintFormat();
  paintChrome();
});

for (const id of ['match-partial', 'auto-create', 'enable-backup', 'enable-history', 'encrypt']) {
  bindToggle(id);
}

document.querySelectorAll('[data-nav]').forEach((button) => {
  button.addEventListener('click', () => {
    const next = button.dataset.nav;
    must('#page-file').hidden = next !== 'file';
    must('#page-signals').hidden = next !== 'signals';
    must('#app').dataset.page = next;
    document.querySelectorAll('[data-nav]').forEach((item) => {
      item.setAttribute('aria-pressed', String(item === button));
    });
  });
});
document.querySelectorAll('.kind-tabs [data-kind]').forEach((button) => {
  button.addEventListener('click', () => {
    kind = button.dataset.kind;
    document.querySelectorAll('.kind-tabs [data-kind]').forEach((item) => {
      item.setAttribute('aria-pressed', String(item === button));
    });
    paintRows();
  });
});
must('#diff-only').addEventListener('change', paintRows);
must('#jump').addEventListener('change', () => {
  const slot = Number(must('#jump').value);
  if (!Number.isInteger(slot) || slot < 1 || slot > SLOT_COUNT) {
    return;
  }
  must('#diff-only').checked = false;
  paintRows();
  rows.querySelector(`.row[data-kind="${kind}"][data-slot="${slot}"]`)?.scrollIntoView({ block: 'center' });
});
must('#use-file').addEventListener('click', useFileValues);
must('#clear-edits').addEventListener('click', clearEdits);
must('#read').addEventListener('click', async () => {
  busy = true;
  paintChrome();
  try {
    await readFile();
  } catch (error) {
    setStatus(error.message);
  } finally {
    busy = false;
    paintChrome();
  }
});
must('#write').addEventListener('click', async () => {
  busy = true;
  paintChrome();
  try {
    await writeFile();
  } catch (error) {
    setStatus(error.message);
  } finally {
    busy = false;
    paintChrome();
  }
});
must('#delete').addEventListener('click', () => openConfirm('delete'));
must('#delete-backup').addEventListener('click', () => openConfirm('delete-backup'));
must('#confirm-no').addEventListener('click', () => {
  confirmAction = null;
  must('#confirm').hidden = true;
});
must('#confirm-yes').addEventListener('click', () => {
  runConfirmed();
});
must('#restore').addEventListener('click', async () => {
  busy = true;
  paintChrome();
  try {
    const backup = await transfer('retrieve', undefined, '.bak');
    if (!backup.exists) {
      setStatus('No backup file.');
      return;
    }
    await transfer('overwrite', backup.text);
    setStatus(`Restored ${remotePath()} from the backup.`);
    await readFile();
  } catch (error) {
    setStatus(error.message);
  } finally {
    busy = false;
    paintChrome();
  }
});
must('#publish').addEventListener('click', async () => {
  try {
    const encoded = await encodeEdits();
    const published = await post('/api/publish', { text: encoded.text, localPath: remotePath() });
    setStatus(`Remote_URL ${published.pullUrl}  Local_Path ${published.localPath}`);
    must('#pull').textContent = `Remote File Sync Remote_URL ${published.pullUrl} and Local_Path ${published.localPath}.`;
  } catch (error) {
    setStatus(error.message);
  }
});
must('#key-show').addEventListener('click', () => {
  const input = must('#encryption-key');
  const showing = input.type === 'text';
  input.type = showing ? 'password' : 'text';
  must('#key-show').textContent = showing ? 'Show' : 'Hide';
});
must('#password-show').addEventListener('click', () => {
  const input = must('#password');
  const showing = input.type === 'text';
  input.type = showing ? 'password' : 'text';
  must('#password-show').textContent = showing ? 'Show' : 'Hide';
});

paintLocation();
paintFormat();
paintToggles();
paintRows();
paintChrome();
