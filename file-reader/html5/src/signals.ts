export const SLOT_COUNT = 64;
export const NAME_LIMIT = 128;
export const TEXT_LIMIT = 256;

export const FORMATS = [
  { code: 1, name: 'Text', ext: '.txt' },
  { code: 2, name: 'CSV', ext: '.csv' },
  { code: 3, name: 'XML', ext: '.xml' },
  { code: 4, name: 'JSON', ext: '.json' },
  { code: 5, name: 'INI', ext: '.ini' },
  { code: 6, name: 'Binary', ext: '.bin' },
] as const;

/**
 * Detected_Format is a SIMPL analog. The library uses -1 and -2, and a 16-bit
 * analog carries those as 65535 and 65534.
 */
export function interpretDetected(raw: number): number {
  if (raw === 65535 || raw === -1) {
    return -1;
  }
  if (raw === 65534 || raw === -2) {
    return -2;
  }
  return raw;
}

export function formatLabel(code: number): string {
  const detected = interpretDetected(code);
  if (detected === -1) {
    return 'Undetected';
  }
  if (detected === -2) {
    return 'Integrity check failed';
  }
  const found = FORMATS.find((item) => item.code === detected);
  if (found) {
    return `${found.name} (${found.ext})`;
  }
  if (detected === 0) {
    return 'Not read';
  }
  return `Code ${detected}`;
}
