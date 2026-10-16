import {
  clampAnalog,
  publishAnalog,
  publishDigital,
  publishSerial,
  pulse,
  subscribeAnalog,
  subscribeDigital,
  subscribeSerial,
} from '../crestron/bridge';
import { formatJoins, Joins, slotJoins } from '../crestron/joins';
import type { CrestronRuntime, ConnectionState } from '../crestron/init';
import { formatLabel, interpretDetected, SLOT_COUNT } from '../signals';
import { startClock } from './clock';

type PageId = 'file' | 'signals';
type Kind = 'digital' | 'analog' | 'serial';
type ConfirmAction = 'delete' | 'delete-backup';

interface SlotState {
  digitalIn: boolean;
  digitalOut: boolean;
  analogIn: number;
  analogOut: number;
  serialIn: string;
  serialOut: string;
}

interface DigitalRow {
  root: HTMLElement;
  button: HTMLButtonElement;
  file: HTMLElement;
}

interface AnalogRow {
  root: HTMLElement;
  input: HTMLInputElement;
  file: HTMLElement;
}

interface SerialRow {
  root: HTMLElement;
  input: HTMLInputElement;
  file: HTMLElement;
}

interface FormatIntent {
  source: 'button' | 'analog';
  value: number;
}

function must<T extends Element>(selector: string): T {
  const element = document.querySelector<T>(selector);
  if (!element) {
    throw new Error(`Missing ${selector}`);
  }
  return element;
}

function blankSlot(): SlotState {
  return {
    digitalIn: false,
    digitalOut: false,
    analogIn: 0,
    analogOut: 0,
    serialIn: '',
    serialOut: '',
  };
}

export function mountApp(runtime: CrestronRuntime): void {
  const fileName = must<HTMLElement>('#file-name');
  const statusLine = must<HTMLElement>('#status-line');
  const sizeFb = must<HTMLElement>('#size-fb');
  const chipExists = must<HTMLElement>('#chip-exists');
  const chipEncrypted = must<HTMLElement>('#chip-encrypted');
  const chipBackup = must<HTMLElement>('#chip-backup');
  const chipBusy = must<HTMLElement>('#chip-busy');
  const readButton = must<HTMLButtonElement>('#read');
  const writeButton = must<HTMLButtonElement>('#write');
  const deleteButton = must<HTMLButtonElement>('#delete');
  const cipDot = must<HTMLElement>('#cip-dot');
  const cipLabel = must<HTMLElement>('#cip-label');
  const nameInput = must<HTMLInputElement>('#name-override');
  const matchButton = must<HTMLButtonElement>('#match-partial');
  const formatInput = must<HTMLInputElement>('#format-analog');
  const detectedLine = must<HTMLElement>('#detected-line');
  const autoButton = must<HTMLButtonElement>('#auto-create');
  const defaultFlash = must<HTMLElement>('#default-flash');
  const rows = must<HTMLElement>('#rows');
  const diffOnlyInput = must<HTMLInputElement>('#diff-only');
  const jumpInput = must<HTMLInputElement>('#jump');
  const noDiffs = must<HTMLElement>('#no-diffs');
  const encryptButton = must<HTMLButtonElement>('#encrypt');
  const keyInput = must<HTMLInputElement>('#encryption-key');
  const keyShow = must<HTMLButtonElement>('#key-show');
  const backupButton = must<HTMLButtonElement>('#enable-backup');
  const historyButton = must<HTMLButtonElement>('#enable-history');
  const restoreButton = must<HTMLButtonElement>('#restore');
  const deleteBackupButton = must<HTMLButtonElement>('#delete-backup');
  const backupState = must<HTMLElement>('#backup-state');
  const encryptedState = must<HTMLElement>('#encrypted-state');
  const confirm = must<HTMLElement>('#confirm');
  const confirmText = must<HTMLElement>('#confirm-text');
  const confirmYes = must<HTMLButtonElement>('#confirm-yes');
  const confirmNo = must<HTMLButtonElement>('#confirm-no');

  const slots = Array.from({ length: SLOT_COUNT }, blankSlot);
  const digitalRows: DigitalRow[] = [];
  const analogRows: AnalogRow[] = [];
  const serialRows: SerialRow[] = [];
  const dirty = {
    digital: new Set<number>(),
    analog: new Set<number>(),
    serial: new Set<number>(),
    name: false,
    key: false,
    match: false,
    encrypt: false,
    backup: false,
    history: false,
    auto: false,
    location: false,
  };

  let joinsLive = false;
  let subscribed = false;
  let page: PageId = 'file';
  let kind: Kind = 'digital';
  let diffOnly = false;
  let busy = false;
  let exists = false;
  let notExist = false;
  let encrypted = false;
  let backupExists = false;
  let fileNameText = '';
  let feedbackText = '';
  let sizeText = '';
  let nameOverride = '';
  let encryptionKey = '';
  let nameCommitted = '';
  let keyCommitted = '';
  let formatFieldCommitted = '';
  let matchPartial = false;
  let encryptOn = false;
  let backupOn = false;
  let historyOn = false;
  let autoOn = false;
  let locationCode = 0;
  let writeFormat = 0;
  let detected = 0;
  const detectedFlags = FORMATS_ON();
  let formatIntent: FormatIntent | null = null;
  let confirmAction: ConfirmAction | null = null;
  let flashTimer = 0;

  startClock(must('#clock-time'), must('#clock-date'));
  buildRows();

  runtime.setConnectionHandler((state, detail) => {
    const live = state === 'online' || state === 'native';
    const becameLive = live && !joinsLive;
    joinsLive = live;
    setConnection(state, detail);
    subscribeAll();
    if (becameLive) {
      flush();
    }
    paintChrome();
  });
  subscribeAll();

  readButton.addEventListener('click', () => sendPulse(Joins.read));
  writeButton.addEventListener('click', () => sendPulse(Joins.write));
  deleteButton.addEventListener('click', () => openConfirm('delete'));
  restoreButton.addEventListener('click', () => sendPulse(Joins.restoreBackup));
  deleteBackupButton.addEventListener('click', () => openConfirm('delete-backup'));
  confirmYes.addEventListener('click', () => {
    if (confirmAction === 'delete') {
      sendPulse(Joins.delete);
    } else if (confirmAction === 'delete-backup') {
      sendPulse(Joins.deleteBackup);
    }
    closeConfirm();
  });
  confirmNo.addEventListener('click', closeConfirm);
  confirm.addEventListener('click', (event) => {
    if (event.target === confirm) {
      closeConfirm();
    }
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      closeConfirm();
    }
  });

  nameInput.addEventListener('change', commitName);
  nameInput.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      nameInput.blur();
    }
  });
  matchButton.addEventListener('click', () => {
    matchPartial = !matchPartial;
    dirty.match = true;
    if (joinsLive) {
      publishDigital(Joins.matchPartial, matchPartial);
    }
    paintToggles();
  });
  autoButton.addEventListener('click', () => {
    autoOn = !autoOn;
    dirty.auto = true;
    if (joinsLive) {
      publishDigital(Joins.autoCreate, autoOn);
    }
    paintToggles();
  });
  encryptButton.addEventListener('click', () => {
    encryptOn = !encryptOn;
    dirty.encrypt = true;
    if (joinsLive) {
      publishDigital(Joins.encrypt, encryptOn);
    }
    paintToggles();
  });
  backupButton.addEventListener('click', () => {
    backupOn = !backupOn;
    dirty.backup = true;
    if (joinsLive) {
      publishDigital(Joins.enableBackup, backupOn);
    }
    paintToggles();
  });
  historyButton.addEventListener('click', () => {
    historyOn = !historyOn;
    dirty.history = true;
    if (joinsLive) {
      publishDigital(Joins.enableBackupHistory, historyOn);
    }
    paintToggles();
  });

  document.querySelectorAll<HTMLButtonElement>('[data-location]').forEach((button) => {
    button.addEventListener('click', () => {
      const code = Number(button.dataset.location);
      if (!Number.isInteger(code) || code < 0 || code > 5) {
        return;
      }
      locationCode = code;
      dirty.location = true;
      if (joinsLive) {
        publishAnalog(Joins.fileLocationOverride, locationCode);
      }
      paintLocation();
    });
  });

  document.querySelectorAll<HTMLButtonElement>('[data-format]').forEach((button) => {
    button.addEventListener('click', () => {
      const code = Number(button.dataset.format);
      if (code < 1 || code > 6) {
        return;
      }
      writeFormat = code;
      formatIntent = { source: 'button', value: code };
      formatFieldCommitted = String(code);
      if (document.activeElement !== formatInput) {
        formatInput.value = String(code);
      }
      if (joinsLive) {
        const item = formatJoins.find((entry) => entry.code === code);
        if (item) {
          pulse(item.press);
        }
      }
      paintFormat();
    });
  });
  formatInput.addEventListener('change', commitFormat);
  formatInput.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      formatInput.blur();
    }
  });

  keyInput.addEventListener('change', commitKey);
  keyInput.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      keyInput.blur();
    }
  });
  keyShow.addEventListener('click', () => {
    const showing = keyInput.type === 'text';
    keyInput.type = showing ? 'password' : 'text';
    keyShow.textContent = showing ? 'Show' : 'Hide';
  });

  document.querySelectorAll<HTMLButtonElement>('[data-nav]').forEach((button) => {
    button.addEventListener('click', () => {
      const next = button.dataset.nav;
      if (next === 'file' || next === 'signals') {
        showPage(next);
      }
    });
  });
  document.querySelectorAll<HTMLButtonElement>('.kind-tabs [data-kind]').forEach((button) => {
    button.addEventListener('click', () => {
      const next = button.dataset.kind;
      if (next === 'digital' || next === 'analog' || next === 'serial') {
        kind = next;
        paintKind();
      }
    });
  });
  diffOnlyInput.addEventListener('change', () => {
    diffOnly = diffOnlyInput.checked;
    paintKind();
  });
  jumpInput.addEventListener('change', () => jumpTo(jumpInput.value));
  jumpInput.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      jumpTo(jumpInput.value);
    }
  });
  must<HTMLButtonElement>('#use-file').addEventListener('click', useFileValues);
  must<HTMLButtonElement>('#clear-edits').addEventListener('click', clearEdits);

  paintChrome();
  paintFormat();
  paintLocation();
  paintToggles();
  paintSlots();
  showPage('file');

  function buildRows(): void {
    for (let index = 0; index < SLOT_COUNT; index += 1) {
      const slot = index + 1;

      const digital = document.createElement('div');
      digital.className = 'row';
      digital.dataset.kind = 'digital';
      digital.dataset.slot = String(slot);
      digital.dataset.diff = 'false';
      const digitalButton = document.createElement('button');
      digitalButton.type = 'button';
      digitalButton.className = 'toggle';
      digitalButton.textContent = 'Off';
      digitalButton.addEventListener('click', () => toggleDigital(index));
      const digitalFile = document.createElement('span');
      digitalFile.className = 'file-value';
      digital.append(indexLabel(slot), digitalButton, digitalFile);
      rows.append(digital);
      digitalRows.push({ root: digital, button: digitalButton, file: digitalFile });

      const analog = document.createElement('div');
      analog.className = 'row';
      analog.dataset.kind = 'analog';
      analog.dataset.slot = String(slot);
      analog.dataset.diff = 'false';
      const analogEdit = document.createElement('span');
      analogEdit.className = 'analog-edit';
      const analogInput = document.createElement('input');
      analogInput.type = 'number';
      analogInput.min = '0';
      analogInput.max = '65535';
      analogInput.step = '1';
      analogInput.inputMode = 'numeric';
      analogInput.value = '0';
      analogInput.addEventListener('change', () => commitAnalog(index));
      analogEdit.append(analogInput);
      const analogFile = document.createElement('span');
      analogFile.className = 'file-value';
      analog.append(indexLabel(slot), analogEdit, analogFile);
      rows.append(analog);
      analogRows.push({
        root: analog,
        input: analogInput,
        file: analogFile,
      });

      const serial = document.createElement('div');
      serial.className = 'row';
      serial.dataset.kind = 'serial';
      serial.dataset.slot = String(slot);
      serial.dataset.diff = 'false';
      const serialInput = document.createElement('input');
      serialInput.type = 'text';
      serialInput.maxLength = 256;
      serialInput.autocomplete = 'off';
      serialInput.spellcheck = false;
      serialInput.addEventListener('change', () => commitSerial(index));
      const serialFile = document.createElement('span');
      serialFile.className = 'file-value';
      serial.append(indexLabel(slot), serialInput, serialFile);
      rows.append(serial);
      serialRows.push({ root: serial, input: serialInput, file: serialFile });
    }
  }

  function subscribeAll(): void {
    if (subscribed || !window.CrComLib) {
      return;
    }
    subscribed = true;
    watchDigital(Joins.exists, (value) => {
      exists = value;
      paintChrome();
    });
    watchDigital(Joins.notExist, (value) => {
      notExist = value;
      paintChrome();
    });
    watchDigital(Joins.busy, (value) => {
      busy = value;
      paintChrome();
    });
    watchDigital(Joins.encrypted, (value) => {
      encrypted = value;
      paintChrome();
    });
    watchDigital(Joins.backupExists, (value) => {
      backupExists = value;
      paintChrome();
    });
    watchDigital(Joins.setDefaultValues, (value) => {
      if (value) {
        showFlash();
      }
    });
    watchSerial(Joins.fileNameFb, (value) => {
      fileNameText = value;
      paintChrome();
    });
    watchSerial(Joins.feedback, (value) => {
      feedbackText = value;
      paintChrome();
    });
    watchSerial(Joins.size, (value) => {
      sizeText = value;
      paintChrome();
    });
    watchAnalog(Joins.detectedFormat, (value) => {
      detected = interpretDetected(value);
      paintFormat();
    });
    formatJoins.forEach((item, index) => {
      watchDigital(item.feedback, (value) => {
        detectedFlags[index] = value;
        paintFormat();
      });
    });
    slotJoins.forEach((slot, index) => {
      watchDigital(slot.digitalOut, (value) => {
        slots[index].digitalOut = value;
        paintDigital(index);
      });
      watchAnalog(slot.analogOut, (value) => {
        slots[index].analogOut = clampAnalog(value);
        paintAnalog(index);
      });
      watchSerial(slot.serialOut, (value) => {
        slots[index].serialOut = value;
        paintSerial(index);
      });
    });
  }

  function flush(): void {
    if (!joinsLive) {
      return;
    }
    if (dirty.name) {
      publishSerial(Joins.fileNameOverride, nameOverride);
    }
    if (dirty.key) {
      publishSerial(Joins.encryptionKey, encryptionKey);
    }
    if (dirty.match) {
      publishDigital(Joins.matchPartial, matchPartial);
    }
    if (dirty.encrypt) {
      publishDigital(Joins.encrypt, encryptOn);
    }
    if (dirty.backup) {
      publishDigital(Joins.enableBackup, backupOn);
    }
    if (dirty.history) {
      publishDigital(Joins.enableBackupHistory, historyOn);
    }
    if (dirty.auto) {
      publishDigital(Joins.autoCreate, autoOn);
    }
    if (dirty.location) {
      publishAnalog(Joins.fileLocationOverride, locationCode);
    }
    for (const index of dirty.digital) {
      publishDigital(slotJoins[index].digitalIn, slots[index].digitalIn);
    }
    for (const index of dirty.analog) {
      publishAnalog(slotJoins[index].analogIn, slots[index].analogIn);
    }
    for (const index of dirty.serial) {
      publishSerial(slotJoins[index].serialIn, slots[index].serialIn);
    }
    if (formatIntent?.source === 'analog') {
      publishAnalog(Joins.formatAnalog, formatIntent.value);
    } else if (formatIntent?.source === 'button') {
      const item = formatJoins.find((entry) => entry.code === formatIntent?.value);
      if (item) {
        pulse(item.press);
      }
    }
  }

  function sendPulse(join: Parameters<typeof pulse>[0]): void {
    if (!joinsLive || busy) {
      return;
    }
    pulse(join);
  }

  function commitName(): void {
    const next = nameInput.value;
    if (next === nameCommitted) {
      return;
    }
    nameCommitted = next;
    nameOverride = next;
    dirty.name = true;
    if (joinsLive) {
      publishSerial(Joins.fileNameOverride, nameOverride);
    }
  }

  function commitKey(): void {
    const next = keyInput.value;
    if (next === keyCommitted) {
      return;
    }
    keyCommitted = next;
    encryptionKey = next;
    dirty.key = true;
    if (joinsLive) {
      publishSerial(Joins.encryptionKey, encryptionKey);
    }
  }

  function commitFormat(): void {
    const raw = formatInput.value.trim();
    if (raw === formatFieldCommitted) {
      return;
    }
    formatFieldCommitted = raw;
    const parsed = raw === '' ? 0 : clampAnalog(Number(raw));
    formatInput.value = raw === '' ? '' : String(parsed);
    formatIntent = { source: 'analog', value: parsed };
    writeFormat = parsed >= 1 && parsed <= 6 ? parsed : 0;
    if (joinsLive) {
      publishAnalog(Joins.formatAnalog, parsed);
    }
    paintFormat();
  }

  function toggleDigital(index: number): void {
    slots[index].digitalIn = !slots[index].digitalIn;
    dirty.digital.add(index);
    if (joinsLive) {
      publishDigital(slotJoins[index].digitalIn, slots[index].digitalIn);
    }
    paintDigital(index);
    paintKind();
  }

  function commitAnalog(index: number): void {
    const parsed = clampAnalog(Number(analogRows[index].input.value));
    analogRows[index].input.value = String(parsed);
    if (parsed === slots[index].analogIn) {
      paintAnalog(index);
      return;
    }
    slots[index].analogIn = parsed;
    dirty.analog.add(index);
    if (joinsLive) {
      publishAnalog(slotJoins[index].analogIn, parsed);
    }
    paintAnalog(index);
    paintKind();
  }

  function commitSerial(index: number): void {
    const next = serialRows[index].input.value.slice(0, 256);
    if (next === slots[index].serialIn) {
      return;
    }
    slots[index].serialIn = next;
    dirty.serial.add(index);
    if (joinsLive) {
      publishSerial(slotJoins[index].serialIn, next);
    }
    paintSerial(index);
    paintKind();
  }

  function useFileValues(): void {
    blurEditor();
    for (let index = 0; index < SLOT_COUNT; index += 1) {
      slots[index].digitalIn = slots[index].digitalOut;
      slots[index].analogIn = slots[index].analogOut;
      slots[index].serialIn = slots[index].serialOut;
      dirty.digital.add(index);
      dirty.analog.add(index);
      dirty.serial.add(index);
    }
    if (joinsLive) {
      flush();
    }
    paintSlots();
  }

  function clearEdits(): void {
    blurEditor();
    for (let index = 0; index < SLOT_COUNT; index += 1) {
      slots[index].digitalIn = false;
      slots[index].analogIn = 0;
      slots[index].serialIn = '';
      dirty.digital.add(index);
      dirty.analog.add(index);
      dirty.serial.add(index);
    }
    if (joinsLive) {
      flush();
    }
    paintSlots();
  }

  function blurEditor(): void {
    const active = document.activeElement;
    if (active instanceof HTMLElement) {
      active.blur();
    }
  }

  function openConfirm(action: ConfirmAction): void {
    if (!joinsLive || busy) {
      return;
    }
    confirmAction = action;
    confirmText.textContent =
      action === 'delete'
        ? 'Delete the file on the processor? The backup file is left in place.'
        : 'Delete the .bak file?';
    confirmYes.textContent = 'Delete';
    confirm.hidden = false;
  }

  function closeConfirm(): void {
    confirmAction = null;
    confirm.hidden = true;
  }

  function showFlash(): void {
    defaultFlash.hidden = false;
    window.clearTimeout(flashTimer);
    flashTimer = window.setTimeout(() => {
      defaultFlash.hidden = true;
    }, 1200);
  }

  function showPage(next: PageId): void {
    page = next;
    for (const id of ['file', 'signals'] as const) {
      must<HTMLElement>(`#page-${id}`).hidden = id !== next;
    }
    document.querySelectorAll<HTMLButtonElement>('[data-nav]').forEach((button) => {
      button.setAttribute('aria-pressed', String(button.dataset.nav === next));
    });
    must<HTMLElement>('#app').dataset.page = page;
  }

  function jumpTo(raw: string): void {
    const slot = Number(raw);
    if (!Number.isInteger(slot) || slot < 1 || slot > SLOT_COUNT) {
      return;
    }
    diffOnly = false;
    diffOnlyInput.checked = false;
    paintKind();
    const row = rows.querySelector<HTMLElement>(`.row[data-kind="${kind}"][data-slot="${slot}"]`);
    row?.scrollIntoView({ block: 'center' });
  }

  function paintChrome(): void {
    const label = fileNameText.trim();
    fileName.textContent = label || 'No file selected';
    fileName.title = label;
    statusLine.textContent = statusText();
    statusLine.title = feedbackText;
    sizeFb.textContent = sizeText.trim() ? sizeText : 'Size —';
    setChip(chipExists, exists ? 'Exists' : notExist ? 'Missing' : 'File', exists ? 'yes' : notExist ? 'no' : 'unknown');
    setChip(chipEncrypted, encrypted ? 'Encrypted' : 'Plain', encrypted ? 'yes' : 'unknown');
    setChip(chipBackup, backupExists ? 'Backup' : 'No backup', backupExists ? 'yes' : 'unknown');
    setChip(chipBusy, busy ? 'Busy' : 'Idle', busy ? 'busy' : 'unknown');
    backupState.textContent = backupExists ? 'Backup is on disk' : 'No backup';
    encryptedState.textContent = encrypted ? 'File header says encrypted' : 'Not encrypted';
    const open = joinsLive && !busy;
    readButton.disabled = !open;
    writeButton.disabled = !open;
    deleteButton.disabled = !open;
    restoreButton.disabled = !open;
    deleteBackupButton.disabled = !open;
    confirmYes.disabled = !open;
  }

  function paintLocation(): void {
    document.querySelectorAll<HTMLButtonElement>('[data-location]').forEach((button) => {
      const selected = dirty.location && Number(button.dataset.location) === locationCode;
      button.setAttribute('aria-pressed', String(selected));
    });
  }

  function paintToggles(): void {
    setPressed(matchButton, matchPartial);
    setPressed(autoButton, autoOn);
    setPressed(encryptButton, encryptOn);
    setPressed(backupButton, backupOn);
    setPressed(historyButton, historyOn);
  }

  function paintFormat(): void {
    document.querySelectorAll<HTMLButtonElement>('[data-format]').forEach((button) => {
      button.setAttribute('aria-pressed', String(Number(button.dataset.format) === writeFormat));
    });
    detectedLine.textContent = `Detected: ${formatLabel(detected)}`;
    document.querySelectorAll<HTMLElement>('[data-detected]').forEach((dot) => {
      const code = Number(dot.dataset.detected);
      const fromAnalog = interpretDetected(detected) === code;
      const fromDigital = detectedFlags[code - 1] === true;
      dot.dataset.on = String(fromAnalog || fromDigital);
    });
  }

  function paintKind(): void {
    rows.dataset.kind = kind;
    rows.dataset.diffOnly = String(diffOnly);
    document.querySelectorAll<HTMLButtonElement>('.kind-tabs [data-kind]').forEach((button) => {
      button.setAttribute('aria-pressed', String(button.dataset.kind === kind));
    });
    const any = slots.some((_, index) => differs(index, kind));
    noDiffs.hidden = !(diffOnly && !any);
  }

  function paintSlots(): void {
    for (let index = 0; index < SLOT_COUNT; index += 1) {
      paintDigital(index);
      paintAnalog(index);
      paintSerial(index);
    }
    paintKind();
  }

  function paintDigital(index: number): void {
    const row = digitalRows[index];
    const on = slots[index].digitalIn;
    row.button.textContent = on ? 'On' : 'Off';
    row.button.setAttribute('aria-pressed', String(on));
    row.file.textContent = slots[index].digitalOut ? 'On' : 'Off';
    row.file.dataset.empty = 'false';
    row.root.dataset.diff = String(slots[index].digitalIn !== slots[index].digitalOut);
  }

  function paintAnalog(index: number): void {
    const row = analogRows[index];
    if (document.activeElement !== row.input) {
      row.input.value = String(slots[index].analogIn);
    }
    row.file.textContent = String(slots[index].analogOut);
    row.file.dataset.empty = 'false';
    row.root.dataset.diff = String(slots[index].analogIn !== slots[index].analogOut);
  }

  function paintSerial(index: number): void {
    const row = serialRows[index];
    if (document.activeElement !== row.input) {
      row.input.value = slots[index].serialIn;
    }
    const file = slots[index].serialOut;
    row.file.textContent = file || 'empty';
    row.file.dataset.empty = String(file.length === 0);
    row.root.dataset.diff = String(slots[index].serialIn !== slots[index].serialOut);
  }

  function differs(index: number, which: Kind): boolean {
    if (which === 'digital') {
      return slots[index].digitalIn !== slots[index].digitalOut;
    }
    if (which === 'analog') {
      return slots[index].analogIn !== slots[index].analogOut;
    }
    return slots[index].serialIn !== slots[index].serialOut;
  }

  function statusText(): string {
    if (feedbackText.trim()) {
      return feedbackText;
    }
    if (!joinsLive) {
      return 'Not connected. Edits stay on this page until the processor is online.';
    }
    return 'Ready';
  }

  function setConnection(state: ConnectionState, detail?: string): void {
    cipDot.dataset.state = state;
    const labels: Record<ConnectionState, string> = {
      native: 'Panel',
      connecting: 'Connecting',
      online: 'Online',
      offline: 'Offline',
      error: 'Error',
    };
    cipLabel.textContent = labels[state];
    cipLabel.title = detail ? `${labels[state]}. ${detail}` : labels[state];
  }
}

function FORMATS_ON(): boolean[] {
  return [false, false, false, false, false, false];
}

function indexLabel(slot: number): HTMLElement {
  const label = document.createElement('span');
  label.className = 'row__index';
  label.textContent = String(slot);
  return label;
}

function setPressed(button: HTMLButtonElement, pressed: boolean): void {
  button.setAttribute('aria-pressed', String(pressed));
}

function setChip(chip: HTMLElement, text: string, state: string): void {
  chip.textContent = text;
  chip.dataset.state = state;
}

function watchDigital(join: Parameters<typeof subscribeDigital>[0], apply: (value: boolean) => void): void {
  try {
    subscribeDigital(join, apply);
  } catch (error) {
    console.warn(join.name, error);
  }
}

function watchAnalog(join: Parameters<typeof subscribeAnalog>[0], apply: (value: number) => void): void {
  try {
    subscribeAnalog(join, apply);
  } catch (error) {
    console.warn(join.name, error);
  }
}

function watchSerial(join: Parameters<typeof subscribeSerial>[0], apply: (value: string) => void): void {
  try {
    subscribeSerial(join, apply);
  } catch (error) {
    console.warn(join.name, error);
  }
}
