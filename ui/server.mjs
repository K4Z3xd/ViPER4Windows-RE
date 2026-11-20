import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PORT = 48192;
const VIPER_CTL = path.resolve(__dirname, '..', 'build', 'viper_ctl.exe');
const DIST_DIR = path.resolve(__dirname, 'dist');
const LIVE_PARAMS_PATH = 'C:\\ProgramData\\ViPERHiRes\\live_params.bin';
const FOOBAR_STATUS_PATH = 'C:\\ProgramData\\ViPERHiRes\\foobar_status.json';
const APO_DIAG_PATH = 'C:\\ProgramData\\ViPERHiRes\\apo_diag.txt';
const IRS_DIR = 'C:\\ProgramData\\ViPERHiRes\\irs';
const ACTIVE_IR_PATH = 'C:\\ProgramData\\ViPERHiRes\\active_ir.txt';
const PRESETS_DIR = 'C:\\ProgramData\\ViPERHiRes\\presets';
const EQ_PRESETS_DIR = 'C:\\ProgramData\\ViPERHiRes\\eq_presets';
const MAGIC_VPHR = 0x56504852;
const PARAMS_VERSION = 4;
const BLOCK_SIZE = 288; // 4-byte seqlock + 284-byte DspParams v4

function listJsonItems(dir) {
  fs.mkdirSync(dir, { recursive: true });
  const files = fs.readdirSync(dir).filter(f => /\.json$/i.test(f)).sort((a, b) => a.localeCompare(b));
  const items = [];
  for (const f of files) {
    try {
      const parsed = JSON.parse(fs.readFileSync(path.join(dir, f), 'utf8'));
      if (parsed && typeof parsed === 'object') {
        items.push({
          ...parsed,
          name: parsed.name || f.replace(/\.json$/i, '')
        });
      }
    } catch {
      // skip malformed preset file
    }
  }
  return items;
}

function saveJsonItem(dir, item) {
  fs.mkdirSync(dir, { recursive: true });
  const safeName = String(item.name || 'Custom Preset').replace(/[^a-zA-Z0-9._\- \u00C0-\u024F\u1E00-\u1EFF]/g, '_').trim() || 'Custom Preset';
  const filePath = path.join(dir, `${safeName}.json`);
  fs.writeFileSync(filePath, JSON.stringify({ ...item, name: safeName }, null, 2), 'utf8');
  return safeName;
}

function listIrsFiles() {
  fs.mkdirSync(IRS_DIR, { recursive: true });
  const files = fs.readdirSync(IRS_DIR)
    .filter(f => /\.(irs|wav)$/i.test(f))
    .sort((a, b) => a.localeCompare(b));
  let active = '';
  try {
    if (fs.existsSync(ACTIVE_IR_PATH)) {
      active = path.basename(fs.readFileSync(ACTIVE_IR_PATH, 'utf8').trim());
    }
  } catch {
    // ignore
  }
  return { files, active };
}

function readParamsDirect() {
  const buf = fs.readFileSync(LIVE_PARAMS_PATH);
  if (buf.length < BLOCK_SIZE || buf.readUInt32LE(4) !== MAGIC_VPHR) {
    throw new Error('Upgrade needed for live_params.bin');
  }
  const eqBandsDb = [];
  for (let i = 0; i < 10; i++) {
    eqBandsDb.push(Math.round(buf.readFloatLE(32 + i * 4) * 100) / 100);
  }
  return {
    sequence: buf.readUInt32LE(12),
    masterEnabled: buf.readUInt32LE(16) !== 0,
    masterGainDb: Math.round(buf.readFloatLE(20) * 100) / 100,
    outputPan: Math.round(buf.readFloatLE(24) * 100) / 100,
    eqEnabled: buf.readUInt32LE(28) !== 0,
    eqBandsDb,
    bassEnabled: buf.readUInt32LE(72) !== 0,
    bassMode: buf.readUInt32LE(76),
    bassFrequencyHz: Math.round(buf.readFloatLE(80) * 100) / 100,
    bassGainDb: Math.round(buf.readFloatLE(84) * 100) / 100,
    clarityEnabled: buf.readUInt32LE(88) !== 0,
    clarityMode: buf.readUInt32LE(92),
    clarityGainDb: Math.round(buf.readFloatLE(96) * 100) / 100,
    tubeEnabled: buf.readUInt32LE(100) !== 0,
    tubeDrive: Math.round(buf.readFloatLE(104) * 100) / 100,
    surroundEnabled: buf.readUInt32LE(108) !== 0,
    surroundDelayMs: Math.round(buf.readFloatLE(112) * 100) / 100,
    stereoWidth: Math.round(buf.readFloatLE(116) * 100) / 100,
    limiterEnabled: buf.readUInt32LE(120) !== 0,
    limiterThresholdDb: Math.round(buf.readFloatLE(124) * 100) / 100,
    // V2 modules
    dynamicEnabled: buf.readUInt32LE(128) !== 0,
    dynamicDevice: buf.readUInt32LE(132),
    dynamicBass: Math.round(buf.readFloatLE(136) * 100) / 100,
    reverbEnabled: buf.readUInt32LE(140) !== 0,
    reverbRoomSize: Math.round(buf.readFloatLE(144) * 100) / 100,
    reverbWidth: Math.round(buf.readFloatLE(148) * 100) / 100,
    reverbDamping: Math.round(buf.readFloatLE(152) * 100) / 100,
    reverbWet: Math.round(buf.readFloatLE(156) * 100) / 100,
    reverbDry: Math.round(buf.readFloatLE(160) * 100) / 100,
    compEnabled: buf.readUInt32LE(164) !== 0,
    compThresholdDb: Math.round(buf.readFloatLE(168) * 100) / 100,
    compRatio: Math.round(buf.readFloatLE(172) * 100) / 100,
    compAttackMs: Math.round(buf.readFloatLE(176) * 100) / 100,
    compReleaseMs: Math.round(buf.readFloatLE(180) * 100) / 100,
    compMakeupDb: Math.round(buf.readFloatLE(184) * 100) / 100,
    cureEnabled: buf.readUInt32LE(188) !== 0,
    cureLevel: buf.readUInt32LE(192),
    // V3 Convolver
    convolverEnabled: buf.readUInt32LE(196) !== 0,
    convolverCrossChannel: Math.round(buf.readFloatLE(200) * 100) / 100,
    convolverWetMix: Math.round(buf.readFloatLE(204) * 100) / 100,
    convolverRevision: buf.readUInt32LE(208),
    // V4 Multi-Band EQ
    eqMode: buf.readUInt32LE(212),
    eqBands18Db: (() => {
      const arr = [];
      for (let i = 0; i < 18; i++) {
        arr.push(Math.round(buf.readFloatLE(216 + i * 4) * 100) / 100);
      }
      return arr;
    })()
  };
}

function writeParamsDirect(p) {
  if (!fs.existsSync(LIVE_PARAMS_PATH) || fs.statSync(LIVE_PARAMS_PATH).size < BLOCK_SIZE) {
    fs.writeFileSync(LIVE_PARAMS_PATH, Buffer.alloc(BLOCK_SIZE));
  }

  const fd = fs.openSync(LIVE_PARAMS_PATH, 'r+');
  try {
    const curBuf = Buffer.alloc(BLOCK_SIZE);
    fs.readSync(fd, curBuf, 0, BLOCK_SIZE, 0);

    let seq = curBuf.readUInt32LE(0);
    if ((seq & 1) !== 0) seq += 1;
    const oddSeq = (seq + 1) >>> 0;
    const evenSeq = (seq + 2) >>> 0;

    // 1. Mark seqlock odd (write in progress)
    const seqBuf = Buffer.alloc(4);
    seqBuf.writeUInt32LE(oddSeq, 0);
    fs.writeSync(fd, seqBuf, 0, 4, 0);

    // 2. Write payload bytes [4..196)
    const payload = Buffer.alloc(BLOCK_SIZE - 4);
    payload.writeUInt32LE(MAGIC_VPHR, 0);
    payload.writeUInt32LE(PARAMS_VERSION, 4);
    payload.writeUInt32LE((evenSeq >>> 1), 8);
    payload.writeUInt32LE(p.masterEnabled ? 1 : 0, 12);
    payload.writeFloatLE(Number(p.masterGainDb ?? 0), 16);
    payload.writeFloatLE(Number(p.outputPan ?? 0), 20);
    payload.writeUInt32LE(p.eqEnabled ? 1 : 0, 24);
    const bands = Array.isArray(p.eqBandsDb) ? p.eqBandsDb : [];
    for (let i = 0; i < 10; i++) {
      payload.writeFloatLE(Number(bands[i] ?? 0), 28 + i * 4);
    }
    payload.writeUInt32LE(p.bassEnabled ? 1 : 0, 68);
    payload.writeUInt32LE(Number(p.bassMode ?? 0) >>> 0, 72);
    payload.writeFloatLE(Number(p.bassFrequencyHz ?? 60), 76);
    payload.writeFloatLE(Number(p.bassGainDb ?? 0), 80);
    payload.writeUInt32LE(p.clarityEnabled ? 1 : 0, 84);
    payload.writeUInt32LE(Number(p.clarityMode ?? 0) >>> 0, 88);
    payload.writeFloatLE(Number(p.clarityGainDb ?? 0), 92);
    payload.writeUInt32LE(p.tubeEnabled ? 1 : 0, 96);
    payload.writeFloatLE(Number(p.tubeDrive ?? 0.25), 100);
    payload.writeUInt32LE(p.surroundEnabled ? 1 : 0, 104);
    payload.writeFloatLE(Number(p.surroundDelayMs ?? 6), 108);
    payload.writeFloatLE(Number(p.stereoWidth ?? 1), 112);
    payload.writeUInt32LE(p.limiterEnabled ? 1 : 0, 116);
    payload.writeFloatLE(Number(p.limiterThresholdDb ?? -0.3), 120);
    // V2 modules (offsets relative to payload start = file offset - 4)
    payload.writeUInt32LE(p.dynamicEnabled ? 1 : 0, 124);
    payload.writeUInt32LE(Number(p.dynamicDevice ?? 0) >>> 0, 128);
    payload.writeFloatLE(Number(p.dynamicBass ?? 25), 132);
    payload.writeUInt32LE(p.reverbEnabled ? 1 : 0, 136);
    payload.writeFloatLE(Number(p.reverbRoomSize ?? 0.35), 140);
    payload.writeFloatLE(Number(p.reverbWidth ?? 0.5), 144);
    payload.writeFloatLE(Number(p.reverbDamping ?? 0.25), 148);
    payload.writeFloatLE(Number(p.reverbWet ?? 0.2), 152);
    payload.writeFloatLE(Number(p.reverbDry ?? 0.85), 156);
    payload.writeUInt32LE(p.compEnabled ? 1 : 0, 160);
    payload.writeFloatLE(Number(p.compThresholdDb ?? -18), 164);
    payload.writeFloatLE(Number(p.compRatio ?? 3), 168);
    payload.writeFloatLE(Number(p.compAttackMs ?? 10), 172);
    payload.writeFloatLE(Number(p.compReleaseMs ?? 120), 176);
    payload.writeFloatLE(Number(p.compMakeupDb ?? 3), 180);
    payload.writeUInt32LE(p.cureEnabled ? 1 : 0, 184);
    payload.writeUInt32LE(Number(p.cureLevel ?? 0) >>> 0, 188);
    // V3 Convolver (file offsets 196..211 -> payload offsets 192..207)
    payload.writeUInt32LE(p.convolverEnabled ? 1 : 0, 192);
    payload.writeFloatLE(Number(p.convolverCrossChannel ?? 0), 196);
    payload.writeFloatLE(Number(p.convolverWetMix ?? 1), 200);
    payload.writeUInt32LE(Number(p.convolverRevision ?? 0) >>> 0, 204);
    // V4 Multi-Band EQ (file offsets 212..287 -> payload offsets 208..283)
    payload.writeUInt32LE(Number(p.eqMode ?? 0) >>> 0, 208);
    const bands18 = Array.isArray(p.eqBands18Db) ? p.eqBands18Db : [];
    for (let i = 0; i < 18; i++) {
      payload.writeFloatLE(Number(bands18[i] ?? 0), 212 + i * 4);
    }

    fs.writeSync(fd, payload, 0, payload.length, 4);

    // 3. Mark seqlock even (write complete)
    seqBuf.writeUInt32LE(evenSeq, 0);
    fs.writeSync(fd, seqBuf, 0, 4, 0);
  } finally {
    fs.closeSync(fd);
  }
  return readParamsDirect();
}

function readLiveStatus(defaultDevice, activeSessions = []) {
  const now = Date.now();
  // 1. Prefer live foobar2000 telemetry if active within last 4 seconds
  try {
    if (fs.existsSync(FOOBAR_STATUS_PATH)) {
      const raw = fs.readFileSync(FOOBAR_STATUS_PATH, 'utf8');
      const fb = JSON.parse(raw);
      if (fb && typeof fb.timestampMs === 'number' && now - fb.timestampMs < 4000) {
        // If foobar2000 appears in Windows Shared Audio Sessions -> Shared Mode; otherwise -> Exclusive Mode
        const isShared = Array.isArray(activeSessions) &&
          activeSessions.some(s => (s.exeName || '').toLowerCase() === 'foobar2000.exe');
        const sr = fb.sampleRate || 0;
        const bits = fb.bitDepth || 0;

        return {
          active: true,
          source: 'foobar2000',
          mode: isShared ? 'Shared Mode' : 'Exclusive Mode',
          sampleRate: sr,
          channels: fb.channels || 2,
          bitDepth: bits,
          bitrateKbps: fb.bitrateKbps || 0,
          codec: fb.codec || '',
          title: fb.title || '',
          artist: fb.artist || '',
          deviceName: defaultDevice?.name || 'USB HiFi DAC',
          isHiRes: sr >= 88200 || bits >= 24,
          zeroResample: !isShared,
          extraApps: []
        };
      }
    }
  } catch {
    // ignore partial reads
  }

  // 2. Fallback to Windows APO (audiodg.exe) telemetry + Active Windows Audio Sessions (Chrome, Edge, Spotify, etc.)
  let apoSr = defaultDevice?.sampleRate || 48000;
  let apoCh = defaultDevice?.channels || 2;
  let apoLocked = false;
  try {
    if (fs.existsSync(APO_DIAG_PATH)) {
      const txt = fs.readFileSync(APO_DIAG_PATH, 'utf8');
      const mSr = txt.match(/SR=(\d+)/);
      const mCh = txt.match(/Ch=(\d+)/);
      if (mSr) {
        apoSr = parseInt(mSr[1], 10);
        apoLocked = true;
      }
      if (mCh) apoCh = parseInt(mCh[1], 10);
    }
  } catch {
    // ignore
  }

  const primary = Array.isArray(activeSessions) && activeSessions.length > 0 ? activeSessions[0] : null;
  const extraApps = Array.isArray(activeSessions) && activeSessions.length > 1
    ? activeSessions.slice(1).map(s => s.appName)
    : [];

  return {
    active: Boolean(primary) || apoLocked,
    source: primary ? primary.appName : 'Windows Audio APO',
    mode: primary
      ? 'Shared Mode'
      : apoLocked
        ? 'Standby'
        : 'Standby',
    sampleRate: apoSr,
    channels: apoCh,
    bitDepth: defaultDevice?.bitsPerSample || 32,
    bitrateKbps: 0,
    codec: '32-BIT FLOAT',
    title: primary?.windowTitle || '',
    artist: '',
    deviceName: defaultDevice?.name || 'Windows Default Audio',
    // Only mark Hi-Res when source truly delivers Hi-Res (browsers/shared mode resample from 44.1/48k)
    isHiRes: false,
    zeroResample: false,
    extraApps
  };
}

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

function readBody(req, maxBytes = 65536) {
  return new Promise((resolve, reject) => {
    let body = '';
    req.on('data', chunk => {
      body += chunk;
      if (body.length > maxBytes) reject(new Error('Body too large'));
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

    if (url.pathname === '/api/status' && req.method === 'GET') {
      let defaultDev = null;
      let sessions = [];
      try {
        const [devJson, sessJson] = await Promise.all([
          runViperCtl(['devices']),
          runViperCtl(['sessions'])
        ]);
        const list = JSON.parse(devJson || '[]');
        defaultDev = list.find(d => d.isDefault) || list[0] || null;
        sessions = JSON.parse(sessJson || '[]');
      } catch {
        // ignore
      }
      sendJson(res, 200, readLiveStatus(defaultDev, sessions));
      return;
    }

    if (url.pathname === '/api/presets' && req.method === 'GET') {
      sendJson(res, 200, listJsonItems(PRESETS_DIR));
      return;
    }

    if (url.pathname === '/api/presets/save' && req.method === 'POST') {
      const raw = await readBody(req, 2 * 1024 * 1024);
      const body = JSON.parse(raw);
      const savedName = saveJsonItem(PRESETS_DIR, body);
      sendJson(res, 200, { savedName, presets: listJsonItems(PRESETS_DIR) });
      return;
    }

    if (url.pathname === '/api/presets/import' && req.method === 'POST') {
      const raw = await readBody(req, 8 * 1024 * 1024);
      const { items } = JSON.parse(raw);
      const imported = [];
      if (Array.isArray(items)) {
        for (const item of items) {
          if (item && typeof item === 'object') {
            imported.push(saveJsonItem(PRESETS_DIR, item));
          }
        }
      }
      sendJson(res, 200, { imported, presets: listJsonItems(PRESETS_DIR) });
      return;
    }

    if (url.pathname === '/api/eq-presets' && req.method === 'GET') {
      sendJson(res, 200, listJsonItems(EQ_PRESETS_DIR));
      return;
    }

    if (url.pathname === '/api/eq-presets/save' && req.method === 'POST') {
      const raw = await readBody(req, 1024 * 1024);
      const body = JSON.parse(raw);
      const savedName = saveJsonItem(EQ_PRESETS_DIR, body);
      sendJson(res, 200, { savedName, presets: listJsonItems(EQ_PRESETS_DIR) });
      return;
    }

    if (url.pathname === '/api/eq-presets/import' && req.method === 'POST') {
      const raw = await readBody(req, 4 * 1024 * 1024);
      const { items } = JSON.parse(raw);
      const imported = [];
      if (Array.isArray(items)) {
        for (const item of items) {
          if (item && typeof item === 'object') {
            imported.push(saveJsonItem(EQ_PRESETS_DIR, item));
          }
        }
      }
      sendJson(res, 200, { imported, presets: listJsonItems(EQ_PRESETS_DIR) });
      return;
    }

    if (url.pathname === '/api/irs' && req.method === 'GET') {
      sendJson(res, 200, listIrsFiles());
      return;
    }

    if (url.pathname === '/api/irs/upload' && req.method === 'POST') {
      // Allow up to 16 MB base64-encoded .irs / .wav file
      const raw = await readBody(req, 24 * 1024 * 1024);
      const { filename, base64 } = JSON.parse(raw);
      const safeName = path.basename(String(filename || 'custom.irs')).replace(/[^a-zA-Z0-9._\- ]/g, '_');
      if (!/\.(irs|wav)$/i.test(safeName)) {
        sendJson(res, 400, { error: 'Only .irs or .wav files are supported' });
        return;
      }
      fs.mkdirSync(IRS_DIR, { recursive: true });
      const destPath = path.join(IRS_DIR, safeName);
      fs.writeFileSync(destPath, Buffer.from(String(base64 || ''), 'base64'));
      fs.writeFileSync(ACTIVE_IR_PATH, destPath, 'utf8');

      const cur = readParamsDirect();
      const updated = writeParamsDirect({
        ...cur,
        convolverEnabled: true,
        convolverRevision: (cur.convolverRevision || 0) + 1
      });
      sendJson(res, 200, { ...listIrsFiles(), params: updated });
      return;
    }

    if (url.pathname === '/api/irs/select' && req.method === 'POST') {
      const raw = await readBody(req);
      const { filename } = JSON.parse(raw);
      const safeName = path.basename(String(filename || ''));
      const fullPath = path.join(IRS_DIR, safeName);
      if (!safeName || !fs.existsSync(fullPath)) {
        sendJson(res, 404, { error: 'IRS file not found' });
        return;
      }
      fs.writeFileSync(ACTIVE_IR_PATH, fullPath, 'utf8');
      const cur = readParamsDirect();
      const updated = writeParamsDirect({
        ...cur,
        convolverRevision: (cur.convolverRevision || 0) + 1
      });
      sendJson(res, 200, { ...listIrsFiles(), params: updated });
      return;
    }

    if (url.pathname === '/api/params' && req.method === 'GET') {
      try {
        sendJson(res, 200, readParamsDirect());
      } catch {
        const out = await runViperCtl(['get']);
        sendJson(res, 200, out || '{}');
      }
      return;
    }

    if (url.pathname === '/api/params' && req.method === 'POST') {
      const raw = await readBody(req);
      const p = JSON.parse(raw);
      // Direct in-memory-cache binary seqlock write (<0.1ms, strictly ordered, zero process spawn)
      const updated = writeParamsDirect(p);
      sendJson(res, 200, updated);
      return;
    }

    // Serve static production build from dist/
    let filePath = path.join(DIST_DIR, url.pathname === '/' ? 'index.html' : url.pathname);
    if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
      filePath = path.join(DIST_DIR, 'index.html');
    }

    if (fs.existsSync(filePath)) {
      const ext = path.extname(filePath);
      const headers = { 'Content-Type': MIME_TYPES[ext] || 'application/octet-stream' };
      if (ext === '.html') {
        headers['Cache-Control'] = 'no-store, no-cache, must-revalidate';
      }
      res.writeHead(200, headers);
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
