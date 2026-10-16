export const SLOT_COUNT = 64;

export function emptyFile() {
  return {
    digitals: Array.from({ length: SLOT_COUNT }, () => 0),
    analogs: Array.from({ length: SLOT_COUNT }, () => 0),
    serials: Array.from({ length: SLOT_COUNT }, () => ''),
  };
}

function asObject(value) {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    return value;
  }
  return null;
}

function pickArrays(root) {
  const direct = asObject(root);
  if (!direct) {
    return null;
  }
  const nested = asObject(direct.data) ?? asObject(direct.signalData) ?? direct;
  const digitals = nested.digitals ?? nested.Digitals;
  const analogs = nested.analogs ?? nested.Analogs;
  const serials = nested.serials ?? nested.Serials;
  if (!Array.isArray(digitals) && !Array.isArray(analogs) && !Array.isArray(serials)) {
    return null;
  }
  return { digitals, analogs, serials };
}

export function parseJson(text) {
  const parsed = JSON.parse(text);
  const arrays = pickArrays(parsed);
  if (!arrays) {
    throw new Error('JSON has no digitals, analogs, or serials array');
  }
  const file = emptyFile();
  for (let index = 0; index < SLOT_COUNT; index += 1) {
    const digital = arrays.digitals?.[index];
    const analog = arrays.analogs?.[index];
    const serial = arrays.serials?.[index];
    file.digitals[index] = digital === true || digital === 1 ? 1 : 0;
    const analogNumber = Number(analog);
    file.analogs[index] = Number.isFinite(analogNumber)
      ? Math.max(0, Math.min(65535, Math.round(analogNumber)))
      : 0;
    file.serials[index] = serial == null ? '' : String(serial).slice(0, 256);
  }
  return file;
}

export function encodeJson(file) {
  const source = file ?? emptyFile();
  return `${JSON.stringify(
    {
      digitals: source.digitals.slice(0, SLOT_COUNT),
      analogs: source.analogs.slice(0, SLOT_COUNT),
      serials: source.serials.slice(0, SLOT_COUNT),
    },
    null,
    2,
  )}\n`;
}
