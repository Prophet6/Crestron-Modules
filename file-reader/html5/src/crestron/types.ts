export type JoinType = 'b' | 'n' | 's' | 'o';

export interface DigitalJoin {
  type: 'b';
  name: string;
}

export interface AnalogJoin {
  type: 'n';
  name: string;
}

export interface SerialJoin {
  type: 's';
  name: string;
}

export interface ObjectJoin {
  type: 'o';
  name: string;
}

export type Join = DigitalJoin | AnalogJoin | SerialJoin | ObjectJoin;

export interface CrComLibApi {
  publishEvent(type: JoinType | string, signalName: string, value: unknown): void;
  subscribeState(type: JoinType | string, signalName: string, callback: (value: never) => void): string;
  unsubscribeState(type: JoinType | string, signalName: string, subscriptionId: string): void;
  getState?(type: JoinType | string, signalName: string, defaultValue?: unknown): unknown;
  getBooleanSignalValue?(signalName: string, defaultValue?: boolean): boolean | null;
  bridgeReceiveIntegerFromNative?: (join: string, value: number) => void;
  bridgeReceiveBooleanFromNative?: (join: string, value: boolean) => void;
  bridgeReceiveStringFromNative?: (join: string, value: string) => void;
  bridgeReceiveObjectFromNative?: (join: string, value: unknown) => void;
}

declare global {
  interface Window {
    CrComLib: CrComLibApi;
    bridgeReceiveIntegerFromNative?: CrComLibApi['bridgeReceiveIntegerFromNative'];
    bridgeReceiveBooleanFromNative?: CrComLibApi['bridgeReceiveBooleanFromNative'];
    bridgeReceiveStringFromNative?: CrComLibApi['bridgeReceiveStringFromNative'];
    bridgeReceiveObjectFromNative?: CrComLibApi['bridgeReceiveObjectFromNative'];
  }
}

export {};
