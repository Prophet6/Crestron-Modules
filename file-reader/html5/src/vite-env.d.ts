/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_PROCESSOR_HOST?: string;
  readonly VITE_IP_ID?: string;
  readonly VITE_AUTH_TOKEN?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
