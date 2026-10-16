import './styles/main.css';
import { debugEnabled } from './config';
import { initCrestron } from './crestron/init';
import { mountApp } from './ui/app';

async function boot(): Promise<void> {
  if (debugEnabled) {
    const { default: eruda } = await import('eruda');
    eruda.init();
  }

  const runtime = initCrestron();
  mountApp(runtime);
}

boot().catch((error: unknown) => {
  console.error(error);
  document.body.innerHTML = `<pre style="padding:24px;color:#f4f7fb">${String(error)}</pre>`;
});
