import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PORT = 48192;
const VIPER_CTL = path.resolve(__dirname, '..', 'build', 'viper_ctl.exe');
const DIST_DIR = path.resolve(__dirname, 'dist');

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.json': 'application/json; charset=utf-8'
};

function runViperCtl(args) {
  return new Promise((resolve, reject) => {
    execFile(VIPER_CTL, args, { windowsHide: true }, (err, stdout, stderr) => {
      if (err) {
        reject(new Error(stderr || err.message));
        return;
      }
      resolve(stdout.trim());
    });
  });
}

function sendJson(res, status, data) {
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Access-Control-Allow-Origin': '*'
  });
  res.end(typeof data === 'string' ? data : JSON.stringify(data));
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let body = '';
    req.on('data', chunk => {
      body += chunk;
      if (body.length > 65536) reject(new Error('Body too large'));
    });
    req.on('end', () => resolve(body));
    req.on('error', reject);
  });
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url || '/', `http://127.0.0.1:${PORT}`);

  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type'
    });
    res.end();
    return;
  }

  try {
    if (url.pathname === '/api/devices' && req.method === 'GET') {
      const out = await runViperCtl(['devices']);
      sendJson(res, 200, out || '[]');
      return;
    }

    if (url.pathname === '/api/params' && req.method === 'GET') {
      const out = await runViperCtl(['get']);
      sendJson(res, 200, out || '{}');
      return;
    }

    if (url.pathname === '/api/params' && req.method === 'POST') {
      const raw = await readBody(req);
      const p = JSON.parse(raw);
      const args = ['set'];

      if (typeof p.masterEnabled === 'boolean') args.push('--master', p.masterEnabled ? '1' : '0');
      if (typeof p.masterGainDb === 'number') args.push('--gain', String(p.masterGainDb));
      if (typeof p.outputPan === 'number') args.push('--pan', String(p.outputPan));
      if (typeof p.eqEnabled === 'boolean') args.push('--eq', p.eqEnabled ? '1' : '0');
      if (Array.isArray(p.eqBandsDb)) args.push('--eq-bands', p.eqBandsDb.map(Number).join(','));
      if (typeof p.bassEnabled === 'boolean') args.push('--bass', p.bassEnabled ? '1' : '0');
      if (typeof p.bassMode === 'number') args.push('--bass-mode', String(p.bassMode));
      if (typeof p.bassFrequencyHz === 'number') args.push('--bass-freq', String(p.bassFrequencyHz));
      if (typeof p.bassGainDb === 'number') args.push('--bass-gain', String(p.bassGainDb));
      if (typeof p.clarityEnabled === 'boolean') args.push('--clarity', p.clarityEnabled ? '1' : '0');
      if (typeof p.clarityMode === 'number') args.push('--clarity-mode', String(p.clarityMode));
      if (typeof p.clarityGainDb === 'number') args.push('--clarity-gain', String(p.clarityGainDb));
      if (typeof p.tubeEnabled === 'boolean') args.push('--tube', p.tubeEnabled ? '1' : '0');
      if (typeof p.tubeDrive === 'number') args.push('--tube-drive', String(p.tubeDrive));
      if (typeof p.surroundEnabled === 'boolean') args.push('--surround', p.surroundEnabled ? '1' : '0');
      if (typeof p.surroundDelayMs === 'number') args.push('--surround-delay', String(p.surroundDelayMs));
      if (typeof p.stereoWidth === 'number') args.push('--stereo-width', String(p.stereoWidth));
      if (typeof p.limiterEnabled === 'boolean') args.push('--limiter', p.limiterEnabled ? '1' : '0');
      if (typeof p.limiterThresholdDb === 'number') args.push('--limiter-thresh', String(p.limiterThresholdDb));

      const out = await runViperCtl(args);
      sendJson(res, 200, out || '{}');
      return;
    }

    // Serve static production build from dist/
    let filePath = path.join(DIST_DIR, url.pathname === '/' ? 'index.html' : url.pathname);
    if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
      filePath = path.join(DIST_DIR, 'index.html');
    }

    if (fs.existsSync(filePath)) {
      const ext = path.extname(filePath);
      res.writeHead(200, { 'Content-Type': MIME_TYPES[ext] || 'application/octet-stream' });
      fs.createReadStream(filePath).pipe(res);
      return;
    }

    sendJson(res, 404, { error: 'Not found. Run npm run build first.' });
  } catch (err) {
    sendJson(res, 500, { error: err instanceof Error ? err.message : String(err) });
  }
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`ViPERHiRes Console running at http://127.0.0.1:${PORT}`);
});
