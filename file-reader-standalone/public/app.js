const SLOT_COUNT = 64;
const file = {
  digitals: Array.from({ length: SLOT_COUNT }, () => 0),
  analogs: Array.from({ length: SLOT_COUNT }, () => 0),
  serials: Array.from({ length: SLOT_COUNT }, () => ''),
};
const rows = document.querySelector('#rows');
const status = document.querySelector('#status');
let kind = 'digital';

function setStatus(text) {
  status.textContent = text;
}

function paint() {
  document.querySelectorAll('.row').forEach((row) => {
    row.hidden = row.dataset.kind !== kind;
  });
}

function load(next) {
  for (let index = 0; index < SLOT_COUNT; index += 1) {
    file.digitals[index] = next.digitals?.[index] ? 1 : 0;
    file.analogs[index] = Number(next.analogs?.[index] ?? 0);
    file.serials[index] = String(next.serials?.[index] ?? '');
  }
  document.querySelectorAll('.row').forEach((row) => {
    const index = Number(row.dataset.index);
    const control = row.querySelector('input, button');
    if (row.dataset.kind === 'digital') {
      control.textContent = file.digitals[index] ? 'On' : 'Off';
      control.setAttribute('aria-pressed', String(file.digitals[index] === 1));
    } else if (row.dataset.kind === 'analog') {
      control.value = String(file.analogs[index]);
    } else {
      control.value = file.serials[index];
    }
  });
}

for (let index = 0; index < SLOT_COUNT; index += 1) {
  for (const slotKind of ['digital', 'analog', 'serial']) {
    const row = document.createElement('div');
    row.className = 'row';
    row.dataset.kind = slotKind;
    row.dataset.index = String(index);
    row.hidden = slotKind !== 'digital';
    const label = document.createElement('span');
    label.textContent = String(index + 1);
    row.append(label);
    if (slotKind === 'digital') {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = 'Off';
      button.addEventListener('click', () => {
        file.digitals[index] = file.digitals[index] ? 0 : 1;
        button.textContent = file.digitals[index] ? 'On' : 'Off';
        button.setAttribute('aria-pressed', String(file.digitals[index] === 1));
      });
      row.append(button);
    } else if (slotKind === 'analog') {
      const input = document.createElement('input');
      input.type = 'number';
      input.min = '0';
      input.max = '65535';
      input.value = '0';
      input.addEventListener('change', () => {
        const value = Math.max(0, Math.min(65535, Math.round(Number(input.value) || 0)));
        file.analogs[index] = value;
        input.value = String(value);
      });
      row.append(input);
    } else {
      const input = document.createElement('input');
      input.type = 'text';
      input.maxLength = 256;
      input.addEventListener('change', () => {
        file.serials[index] = input.value.slice(0, 256);
      });
      row.append(input);
    }
    rows.append(row);
  }
}

document.querySelectorAll('[data-kind]').forEach((button) => {
  button.addEventListener('click', () => {
    kind = button.dataset.kind;
    document.querySelectorAll('[data-kind]').forEach((item) => {
      item.setAttribute('aria-pressed', String(item === button));
    });
    paint();
  });
});

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

function job(op, text) {
  return {
    op,
    url: document.querySelector('#url').value.trim(),
    user: document.querySelector('#user').value,
    password: document.querySelector('#password').value,
    insecure: document.querySelector('#insecure').checked,
    text,
  };
}

document.querySelector('#check').addEventListener('click', async () => {
  try {
    const result = await post('/api/transfer', job('check'));
    setStatus(result.message);
  } catch (error) {
    setStatus(error.message);
  }
});

document.querySelector('#read').addEventListener('click', async () => {
  try {
    const result = await post('/api/transfer', job('retrieve'));
    if (!result.exists) {
      setStatus(result.message);
      return;
    }
    const parsed = await post('/api/parse', { text: result.text });
    load(parsed.file);
    setStatus(result.message);
  } catch (error) {
    setStatus(error.message);
  }
});

document.querySelector('#write').addEventListener('click', async () => {
  try {
    const encoded = await post('/api/encode', { file });
    const result = await post('/api/transfer', job('overwrite', encoded.text));
    setStatus(result.message);
  } catch (error) {
    setStatus(error.message);
  }
});

document.querySelector('#publish').addEventListener('click', async () => {
  try {
    const encoded = await post('/api/encode', { file });
    const published = await post('/api/publish', { text: encoded.text });
    const pull = document.querySelector('#pull');
    pull.textContent = `Remote File Sync Remote_URL ${published.pullUrl} and Local_Path ${published.localPath}. Retrieve writes that file on the processor.`;
    setStatus('Published for \\user');
  } catch (error) {
    setStatus(error.message);
  }
});

document.querySelector('#download').addEventListener('click', async () => {
  try {
    const encoded = await post('/api/encode', { file });
    const blob = new Blob([encoded.text], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = 'config.json';
    link.click();
    URL.revokeObjectURL(link.href);
    setStatus('Downloaded config.json');
  } catch (error) {
    setStatus(error.message);
  }
});

document.querySelector('#open').addEventListener('change', async (event) => {
  const selected = event.target.files?.[0];
  if (!selected) {
    return;
  }
  try {
    const parsed = await post('/api/parse', { text: await selected.text() });
    load(parsed.file);
    setStatus(`Opened ${selected.name}`);
  } catch (error) {
    setStatus(error.message);
  }
});
