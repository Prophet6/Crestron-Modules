import { getWebXPanel, runsInContainerApp } from '@crestron/ch5-webxpanel';
import { authToken, ipId, processorHost, tokenUrl } from '../config';
import { ensureIncomingDigitalHook } from './bridge';
import type { CrComLibApi } from './types';

export type ConnectionState = 'native' | 'connecting' | 'online' | 'offline' | 'error';

export interface CrestronRuntime {
  isWebXPanel: boolean;
  setConnectionHandler(handler: (state: ConnectionState, detail?: string) => void): void;
}

function attachNativeBridges(): boolean {
  const api = window.CrComLib as CrComLibApi | undefined;
  if (!api) {
    console.warn('CrComLib not on window. The page keeps local edits until the library loads.');
    return false;
  }

  window.bridgeReceiveIntegerFromNative = api.bridgeReceiveIntegerFromNative;
  window.bridgeReceiveBooleanFromNative = api.bridgeReceiveBooleanFromNative;
  window.bridgeReceiveStringFromNative = api.bridgeReceiveStringFromNative;
  window.bridgeReceiveObjectFromNative = api.bridgeReceiveObjectFromNative;
  ensureIncomingDigitalHook();
  return true;
}

export function initCrestron(): CrestronRuntime {
  attachNativeBridges();

  const { isActive, WebXPanel, WebXPanelEvents } = getWebXPanel(!runsInContainerApp());
  let handler: (state: ConnectionState, detail?: string) => void = () => undefined;
  const ipLabel = ipId.replace(/^0x/i, '');
  const notify = (state: ConnectionState, detail?: string) => handler(state, detail);

  if (!isActive) {
    return {
      isWebXPanel: false,
      setConnectionHandler(next) {
        handler = next;
        notify('native', `${processorHost} · IP-ID ${ipLabel}`);
      },
    };
  }

  const configuration: { host: string; ipId: string; tokenUrl: string; authToken?: string } = {
    host: processorHost,
    ipId,
    tokenUrl,
  };
  if (authToken) {
    configuration.authToken = authToken;
  }

  console.info('Initializing WebXPanel', {
    host: configuration.host,
    ipId: configuration.ipId,
    tokenUrl: configuration.tokenUrl,
    authToken: configuration.authToken ? '(present)' : '(none)',
  });
  notify('connecting', `${processorHost} · IP-ID ${ipLabel}`);
  WebXPanel.initialize(configuration);

  const on = (name: string, listener: EventListener) => {
    window.addEventListener(name, listener);
  };
  const certHint = 'Accept the processor certificate on port 49200';
  let cipJoined = false;

  on(WebXPanelEvents.CONNECT_CIP, () => {
    cipJoined = true;
    notify('online', `${processorHost} · IP-ID ${ipLabel}`);
  });
  on(WebXPanelEvents.DISCONNECT_CIP, (event: Event) => {
    const detail = (event as CustomEvent<{ reason?: string }>).detail;
    notify('offline', detail?.reason ?? 'CIP disconnected');
  });
  on(WebXPanelEvents.DISCONNECT_WS, (event: Event) => {
    const detail = (event as CustomEvent<{ reason?: string }>).detail;
    notify('offline', detail?.reason ?? (cipJoined ? 'WebSocket disconnected' : certHint));
  });
  on(WebXPanelEvents.ERROR_WS, () => notify('error', cipJoined ? 'WebSocket error' : certHint));
  on(WebXPanelEvents.FETCH_TOKEN_FAILED, () => {
    notify('error', `Token failed · ${tokenUrl}`);
  });
  on(WebXPanelEvents.AUTHENTICATION_REQUIRED, () => {
    notify('error', `Sign in at https://${processorHost}`);
  });
  on(WebXPanelEvents.AUTHENTICATION_FAILED, () => {
    notify('error', 'Authentication failed');
  });
  on(WebXPanelEvents.INVALID_CREDENTIALS, () => {
    notify('error', 'Invalid credentials');
  });
  on(WebXPanelEvents.NOT_AUTHORIZED, (event: Event) => {
    const detail = (event as CustomEvent<{ redirectTo?: string }>).detail;
    notify('error', 'Not authorized');
    if (detail?.redirectTo) {
      window.location.assign(detail.redirectTo);
    }
  });
  on(WebXPanelEvents.WEB_WORKER_FAILED, () => notify('error', 'Web worker failed'));

  return {
    isWebXPanel: true,
    setConnectionHandler(next) {
      handler = next;
      notify('connecting', `${processorHost} · IP-ID ${ipLabel}`);
    },
  };
}
