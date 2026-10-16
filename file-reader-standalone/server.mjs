import http from 'node:http';
import https from 'node:https';
import net from 'node:net';
import { readFile } from 'node:fs/promises';
import { dirname, extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import { encodeJson, parseJson } from './codec.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), 'public');
const port = Number(process.env.PORT || 5180);
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
};

function sendJson(response, status, body) {
  const payload = JSON.stringify(body);
  response.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(payload),
  });
  response.end(payload);
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    request.on('data', (chunk) => chunks.push(chunk));
    request.on('end', () => {
      const raw = Buffer.concat(chunks).toString('utf8');
      if (!raw) {
        resolve({});
        return;
      }
      try {
        resolve(JSON.parse(raw));
      } catch (error) {
        reject(error);
      }
    });
    request.on('error', reject);
  });
}

function webRequest({ target, method, body, user, password, insecure }) {
  const lib = target.protocol === 'https:' ? https : http;
  const headers = {};
  if (user) {
    headers.Authorization = `Basic ${Buffer.from(`${user}:${password ?? ''}`).toString('base64')}`;
  }
  if (body != null) {
    headers['Content-Type'] = 'application/json; charset=utf-8';
    headers['Content-Length'] = Buffer.byteLength(body);
  }
  return new Promise((resolve, reject) => {
    const req = lib.request(
      {
        protocol: target.protocol,
        hostname: target.hostname,
        port: target.port || undefined,
        path: `${target.pathname}${target.search}`,
        method,
        headers,
        rejectUnauthorized: !insecure,
        timeout: 20000,
      },
      (res) => {
        const chunks = [];
        res.on('data', (chunk) => chunks.push(chunk));
        res.on('end', () => {
          resolve({
            status: res.statusCode ?? 0,
            body: Buffer.concat(chunks).toString('utf8'),
          });
        });
      },
    );
    req.on('timeout', () => req.destroy(new Error('Timed out')));
    req.on('error', reject);
    if (body != null) {
      req.write(body);
    }
    req.end();
  });
}

function ftpCommand(socket, line) {
  return new Promise((resolve, reject) => {
    let buffer = '';
    const onData = (chunk) => {
      buffer += chunk.toString('latin1');
      const lines = buffer.split('\r\n').filter((item) => item.length > 0);
      const last = lines.at(-1) ?? '';
      if (/^\d{3} /.test(last)) {
        socket.off('data', onData);
        resolve({ code: Number(last.slice(0, 3)), text: lines.join('\n') });
      }
    };
    socket.on('data', onData);
    socket.resume();
    socket.once('error', reject);
    if (line) {
      socket.write(`${line}\r\n`);
    }
  });
}

function parsePassive(text) {
  const match = text.match(/\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)/);
  if (!match) {
    throw new Error('FTP server did not give a passive address');
  }
  const numbers = match.slice(1).map(Number);
  return {
    host: numbers.slice(0, 4).join('.'),
    port: numbers[4] * 256 + numbers[5],
  };
}

function connectFtp({ host, port, user, password }) {
  return new Promise((resolve, reject) => {
    const socket = net.connect({ host, port });
    socket.pause();
    socket.once('connect', async () => {
      try {
        const banner = await ftpCommand(socket, '');
        if (banner.code !== 220) {
          throw new Error(banner.text);
        }
        const login = await ftpCommand(socket, `USER ${user || 'anonymous'}`);
        if (login.code === 331) {
          const pass = await ftpCommand(socket, `PASS ${password ?? ''}`);
          if (pass.code !== 230) {
            throw new Error(pass.text || 'Login was rejected');
          }
        } else if (login.code !== 230) {
          throw new Error(login.text || 'Login was rejected');
        }
        const type = await ftpCommand(socket, 'TYPE I');
        if (type.code !== 200) {
          throw new Error(type.text);
        }
        resolve(socket);
      } catch (error) {
        socket.destroy();
        reject(error);
      }
    });
    socket.setTimeout(20000, () => {
      socket.destroy(new Error('Timed out'));
    });
    socket.on('error', reject);
  });
}

async function ftpData(control, command) {
  const passive = await ftpCommand(control, 'PASV');
  if (passive.code !== 227) {
    throw new Error(passive.text);
  }
  const address = parsePassive(passive.text);
  const data = await new Promise((resolve, reject) => {
    const socket = net.connect(address, () => resolve(socket));
    socket.once('error', reject);
  });
  const replyPromise = ftpCommand(control, command);
  const chunks = [];
  const body = await new Promise((resolve, reject) => {
    data.on('data', (chunk) => chunks.push(chunk));
    data.on('error', reject);
    data.on('end', () => resolve(Buffer.concat(chunks)));
  });
  const reply = await replyPromise;
  return { reply, body };
}

function ftpPath(target) {
  const path = decodeURIComponent(target.pathname || '/');
  return path.startsWith('/') ? path : `/${path}`;
}

async function ftpTransfer(job) {
  const target = new URL(job.url);
  const control = await connectFtp({
    host: target.hostname,
    port: Number(target.port || 21),
    user: job.user,
    password: job.password,
  });
  const path = ftpPath(target);
  try {
    if (job.op === 'check') {
      const reply = await ftpCommand(control, `SIZE ${path}`);
      await ftpCommand(control, 'QUIT');
      if (reply.code === 213) {
        return { ok: true, exists: true, message: 'Remote file found' };
      }
      if (reply.code === 550) {
        return { ok: true, exists: false, message: 'Remote file not found' };
      }
      throw new Error(reply.text);
    }
    if (job.op === 'retrieve') {
      const result = await ftpData(control, `RETR ${path}`);
      await ftpCommand(control, 'QUIT');
      if (result.reply.code !== 226 && result.reply.code !== 250) {
        throw new Error(result.reply.text || 'Retrieve failed');
      }
      return { ok: true, exists: true, message: 'Retrieved', text: result.body.toString('utf8') };
    }
    const payload = Buffer.from(job.text ?? '', 'utf8');
    const passive = await ftpCommand(control, 'PASV');
    if (passive.code !== 227) {
      throw new Error(passive.text);
    }
    const data = await new Promise((resolve, reject) => {
      const socket = net.connect(parsePassive(passive.text), () => resolve(socket));
      socket.once('error', reject);
    });
    const replyPromise = ftpCommand(control, `STOR ${path}`);
    await new Promise((resolve, reject) => {
      data.on('error', reject);
      data.end(payload, resolve);
    });
    const reply = await replyPromise;
    await ftpCommand(control, 'QUIT');
    if (reply.code !== 226 && reply.code !== 250) {
      throw new Error(reply.text || 'Overwrite failed');
    }
    return { ok: true, exists: true, message: 'Remote file replaced' };
  } finally {
    control.destroy();
  }
}

async function webTransfer(job) {
  const target = new URL(job.url);
  if (job.op === 'check') {
    const result = await webRequest({ ...job, target, method: 'HEAD' });
    if (result.status === 404 || result.status === 410) {
      return { ok: true, exists: false, message: 'Remote file not found' };
    }
    if (result.status >= 200 && result.status < 300) {
      return { ok: true, exists: true, message: 'Remote file found' };
    }
    throw new Error(`HTTP ${result.status}`);
  }
  if (job.op === 'retrieve') {
    const result = await webRequest({ ...job, target, method: 'GET' });
    if (result.status === 404 || result.status === 410) {
      return { ok: true, exists: false, message: 'Remote file not found', text: '' };
    }
    if (result.status < 200 || result.status >= 300) {
      throw new Error(`HTTP ${result.status}`);
    }
    return { ok: true, exists: true, message: 'Retrieved', text: result.body };
  }
  const result = await webRequest({ ...job, target, method: 'PUT', body: job.text ?? '' });
  if (result.status < 200 || result.status >= 300) {
    throw new Error(`HTTP ${result.status}`);
  }
  return { ok: true, exists: true, message: 'Remote file replaced' };
}

async function transfer(job) {
  const target = new URL(job.url);
  if (target.protocol === 'ftp:') {
    return ftpTransfer(job);
  }
  if (target.protocol === 'http:' || target.protocol === 'https:') {
    return webTransfer(job);
  }
  throw new Error('Use an http, https, or ftp URL');
}

const server = http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url || '/', `http://${request.headers.host}`);
    if (request.method === 'POST' && url.pathname === '/api/encode') {
      const body = await readBody(request);
      sendJson(response, 200, { text: encodeJson(body.file) });
      return;
    }
    if (request.method === 'POST' && url.pathname === '/api/parse') {
      const body = await readBody(request);
      sendJson(response, 200, { file: parseJson(String(body.text ?? '')) });
      return;
    }
    if (request.method === 'POST' && url.pathname === '/api/transfer') {
      const body = await readBody(request);
      const result = await transfer({
        op: body.op,
        url: body.url,
        user: body.user,
        password: body.password,
        insecure: body.insecure === true,
        text: body.text,
      });
      sendJson(response, 200, result);
      return;
    }
    const requested = (url.pathname === '/' ? 'index.html' : url.pathname).replace(/^[/\\]+/, '');
    const filePath = normalize(join(root, requested));
    if (!filePath.startsWith(root)) {
      sendJson(response, 404, { error: 'Not found' });
      return;
    }
    const bytes = await readFile(filePath);
    response.writeHead(200, { 'Content-Type': types[extname(filePath)] || 'application/octet-stream' });
    response.end(bytes);
  } catch (error) {
    sendJson(response, 400, { error: error instanceof Error ? error.message : 'Request failed' });
  }
});

server.listen(port, () => {
  console.log(`File Reader standalone at http://localhost:${port}`);
});
