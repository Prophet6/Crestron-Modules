import type { AnalogJoin, CrComLibApi, DigitalJoin, Join, SerialJoin } from './types';

function lib(): CrComLibApi | undefined {
  return window.CrComLib;
}

type IncomingDigital = (name: string, value: boolean) => void;
const incomingDigitalHandlers: IncomingDigital[] = [];
let bridgeHooked = false;

function hookIncomingBooleans(): void {
  const api = lib();
  if (!api?.bridgeReceiveBooleanFromNative || bridgeHooked) {
    return;
  }
  const original = api.bridgeReceiveBooleanFromNative.bind(api);
  const wrapped = (name: string, value: boolean) => {
    original(name, value);
    const open = Boolean(value);
    for (const handler of incomingDigitalHandlers) {
      handler(String(name), open);
    }
  };
  api.bridgeReceiveBooleanFromNative = wrapped;
  window.bridgeReceiveBooleanFromNative = wrapped;
  bridgeHooked = true;
}

export function ensureIncomingDigitalHook(): void {
  hookIncomingBooleans();
}

export function onIncomingDigital(handler: IncomingDigital): void {
  hookIncomingBooleans();
  incomingDigitalHandlers.push(handler);
}

/** Only an explicit high counts. The string "false" is low. */
export function asDigital(value: unknown): boolean {
  if (value === true || value === 1) {
    return true;
  }
  if (typeof value === 'string') {
    const text = value.trim().toLowerCase();
    return text === '1' || text === 'true';
  }
  return false;
}

export function asAnalog(value: unknown): number {
  const number = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(number)) {
    return 0;
  }
  return Math.round(number);
}

export function asText(value: unknown): string {
  if (value == null) {
    return '';
  }
  return String(value);
}

export function publishDigital(join: DigitalJoin, value: boolean): void {
  lib()?.publishEvent('b', join.name, value);
}

const pulseTimers = new Map<string, number>();

/** High, then low after 100ms. Back-to-back calls restart that window. */
export function pulse(join: DigitalJoin): void {
  publishDigital(join, true);
  const pending = pulseTimers.get(join.name);
  if (pending !== undefined) {
    window.clearTimeout(pending);
  }
  pulseTimers.set(
    join.name,
    window.setTimeout(() => {
      publishDigital(join, false);
      pulseTimers.delete(join.name);
    }, 100),
  );
}

export function publishAnalog(join: AnalogJoin, value: number): void {
  lib()?.publishEvent('n', join.name, value);
}

export function publishSerial(join: SerialJoin, value: string): void {
  lib()?.publishEvent('s', join.name, value);
}

export function subscribeDigital(join: DigitalJoin, callback: (value: boolean) => void): void {
  hookIncomingBooleans();
  const api = lib();
  if (!api) {
    return;
  }
  const wrapped = (value: never) => {
    callback(asDigital(value));
  };
  api.subscribeState('b', join.name, wrapped);
  try {
    api.subscribeState('boolean', join.name, wrapped);
  } catch {
    /* older CrComLib only accepts 'b' */
  }
}

export function subscribeAnalog(join: AnalogJoin, callback: (value: number) => void): void {
  lib()?.subscribeState('n', join.name, ((value: never) => {
    callback(asAnalog(value));
  }) as (value: never) => void);
}

export function subscribeSerial(join: SerialJoin, callback: (value: string) => void): void {
  lib()?.subscribeState('s', join.name, ((value: never) => {
    callback(asText(value));
  }) as (value: never) => void);
}

export function unsubscribe(join: Join, subscriptionId: string): void {
  lib()?.unsubscribeState(join.type, join.name, subscriptionId);
}

export function clampAnalog(value: number): number {
  if (!Number.isFinite(value)) {
    return 0;
  }
  return clamp(Math.round(value), 0, 65535);
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
