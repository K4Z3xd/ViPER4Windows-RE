import React, { useEffect, useRef, useState } from 'react';
import {
  Activity,
  AudioLines,
  Check,
  Copy,
  Ear,
  Flame,
  FolderOpen,
  Gauge,
  Headphones,
  Pin,
  PinOff,
  Power,
  Radio,
  RefreshCw,
  RotateCcw,
  Save,
  ShieldCheck,
  Sliders,
  Sparkles,
  Upload,
  Volume2,
  Waves,
  Zap
} from 'lucide-react';
import {
  AudioDevice,
  BUILTIN_EQ_PRESETS,
  BUILTIN_PRESETS,
  DEFAULT_PARAMS,
  DspParams,
  EQ_LABELS_10,
  EQ_LABELS_18,
  EqPreset,
  LiveSourceStatus,
  Preset
} from './types';
import { InfoTip } from './components/InfoTip';

function Toggle({
  checked,
  onChange,
  label
}: {
  checked: boolean;
  onChange: (val: boolean) => void;
  label: string;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full border transition-colors duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white ${
        checked
          ? 'border-emerald-400/60 bg-emerald-500/25'
          : 'border-zinc-700 bg-zinc-900'
      }`}
    >
      <span
        className={`pointer-events-none block h-4 w-4 rounded-full transition-transform duration-200 ${
          checked
            ? 'translate-x-5.5 bg-emerald-400 shadow-[0_0_10px_rgba(52,211,153,0.7)]'
            : 'translate-x-1 bg-zinc-500'
        }`}
      />
    </button>
  );
}

function EqCurveSvg({ bands, enabled }: { bands: number[]; enabled: boolean }) {
  const width = 640;
  const height = 110;
  const padX = 28;
  const padY = 14;
  const usableW = width - padX * 2;
  const usableH = height - padY * 2;

  const points = bands.map((db, idx) => {
    const x = padX + (idx / (bands.length - 1)) * usableW;
    const norm = (db + 12) / 24; // 0..1
    const y = padY + (1 - norm) * usableH;
    return { x, y };
  });

  // Smooth Catmull-Rom-like cubic bezier path
  let d = `M ${points[0].x} ${points[0].y}`;
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[i];
    const p1 = points[i + 1];
    const midX = (p0.x + p1.x) / 2;
    d += ` C ${midX} ${p0.y}, ${midX} ${p1.y}, ${p1.x} ${p1.y}`;
  }

  const areaD = `${d} L ${points[points.length - 1].x} ${height - padY} L ${points[0].x} ${height - padY} Z`;
  const zeroY = padY + usableH / 2;

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      className="h-28 w-full overflow-visible rounded-lg border border-zinc-800/80 bg-black/70"
      aria-label="10-Band Frequency Response Curve"
    >
      <defs>
        <linearGradient id="eqFill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={enabled ? '#34d399' : '#71717a'} stopOpacity="0.28" />
          <stop offset="100%" stopColor={enabled ? '#34d399' : '#71717a'} stopOpacity="0.0" />
        </linearGradient>
      </defs>

      {/* Grid lines: +6, 0, -6 dB */}
      {[0.25, 0.5, 0.75].map((ratio, idx) => {
        const y = padY + ratio * usableH;
        return (
          <line
            key={idx}
            x1={padX}
            y1={y}
            x2={width - padX}
            y2={y}
            stroke={ratio === 0.5 ? '#3f3f46' : '#18181b'}
            strokeDasharray={ratio === 0.5 ? '4 4' : undefined}
            strokeWidth="1"
          />
        );
      })}

      <path d={areaD} fill="url(#eqFill)" />
      <path
        d={d}
        fill="none"
        stroke={enabled ? '#34d399' : '#52525b'}
        strokeWidth="2.2"
        strokeLinecap="round"
      />

      {points.map((pt, idx) => (
        <circle
          key={idx}
          cx={pt.x}
          cy={pt.y}
          r="3.2"
          fill={enabled ? '#fafafa' : '#52525b'}
          stroke={enabled ? '#10b981' : '#27272a'}
          strokeWidth="1.5"
        />
      ))}

      <text x="6" y={zeroY + 3} fill="#71717a" fontSize="9" fontFamily="monospace">
        0dB
      </text>
    </svg>
  );
}

export default function App() {
  const [params, setParams] = useState<DspParams>(DEFAULT_PARAMS);
  const [devices, setDevices] = useState<AudioDevice[]>([]);
  const [liveStatus, setLiveStatus] = useState<LiveSourceStatus | null>(null);
  const [irsFiles, setIrsFiles] = useState<string[]>([]);
  const [activeIrs, setActiveIrs] = useState<string>('');
  const [customPresets, setCustomPresets] = useState<Preset[]>([]);
  const [selectedPresetName, setSelectedPresetName] = useState<string>('Flat Reference');
  const [pinnedPresetNames, setPinnedPresetNames] = useState<string[]>(() => {
    try {
      const saved = localStorage.getItem('v4w_pinned_presets');
      if (saved) return JSON.parse(saved);
    } catch {
      // ignore
    }
    return BUILTIN_PRESETS.map(p => p.name);
  });
  const [customEqPresets, setCustomEqPresets] = useState<EqPreset[]>([]);
  const [selectedEqPresetName, setSelectedEqPresetName] = useState<string>('Flat');
  const [pinnedModules, setPinnedModules] = useState<string[]>(() => {
    try {
      const saved = localStorage.getItem('v4w_pinned_modules');
      if (saved) return JSON.parse(saved);
    } catch {
      // ignore
    }
    return [];
  });
  const [activeTab, setActiveTab] = useState<'dsp' | 'devices'>('dsp');
  const [syncStatus, setSyncStatus] = useState<'connected' | 'syncing' | 'offline'>('syncing');
  const [copiedCmd, setCopiedCmd] = useState<string | null>(null);
  const inFlightRef = useRef<boolean>(false);
  const pendingParamsRef = useRef<DspParams | null>(null);

  const allPresets: Preset[] = [
    ...BUILTIN_PRESETS,
    ...customPresets.filter(cp => !BUILTIN_PRESETS.some(bp => bp.name === cp.name))
  ];
  const pinnedPresets = pinnedPresetNames
    .map(name => allPresets.find(p => p.name === name))
    .filter((p): p is Preset => Boolean(p));

  const allEqPresets: EqPreset[] = [
    ...BUILTIN_EQ_PRESETS,
    ...customEqPresets.filter(cp => !BUILTIN_EQ_PRESETS.some(bp => bp.name === cp.name))
  ];

  const togglePinPreset = (name: string) => {
    setPinnedPresetNames(prev => {
      const next = prev.includes(name) ? prev.filter(n => n !== name) : [...prev, name];
      try {
        localStorage.setItem('v4w_pinned_presets', JSON.stringify(next));
      } catch {
        // ignore
      }
      return next;
    });
  };

  const togglePinModule = (moduleId: string) => {
    setPinnedModules(prev => {
      const next = prev.includes(moduleId) ? prev.filter(id => id !== moduleId) : [...prev, moduleId];
      try {
        localStorage.setItem('v4w_pinned_modules', JSON.stringify(next));
      } catch {
        // ignore
      }
      return next;
    });
  };

  const getModuleOrder = (moduleId: string, defaultIdx: number) => {
    const pinIdx = pinnedModules.indexOf(moduleId);
    return pinIdx >= 0 ? pinIdx : 100 + defaultIdx;
  };

  const renderModulePin = (moduleId: string) => {
    const isPinned = pinnedModules.includes(moduleId);
    return (
      <button
        type="button"
        onClick={() => togglePinModule(moduleId)}
        title={isPinned ? 'Unpin module' : 'Pin module to top'}
        aria-label={isPinned ? `Unpin ${moduleId}` : `Pin ${moduleId} to top`}
        className={`inline-flex h-6 w-6 cursor-pointer items-center justify-center rounded-md border transition-colors ${
          isPinned
            ? 'border-emerald-500/50 bg-emerald-500/15 text-emerald-400'
            : 'border-zinc-800 bg-zinc-950 text-zinc-500 hover:border-zinc-700 hover:text-zinc-200'
        }`}
      >
        <Pin className="h-3.5 w-3.5" />
      </button>
    );
  };

  const loadStatus = async () => {
    try {
      const res = await fetch('/api/status');
      if (res.ok) {
        setLiveStatus(await res.json());
      }
    } catch {
      // ignore transient poll errors
    }
  };

  const loadAll = async () => {
    try {
      const [devRes, paramRes, statusRes, irsRes, presetsRes, eqPresetsRes] = await Promise.all([
        fetch('/api/devices'),
        fetch('/api/params'),
        fetch('/api/status'),
        fetch('/api/irs'),
        fetch('/api/presets'),
        fetch('/api/eq-presets')
      ]);
      if (devRes.ok) {
        setDevices(await devRes.json());
      }
      if (paramRes.ok) {
        const p = await paramRes.json();
        setParams({ ...DEFAULT_PARAMS, ...p });
      }
      if (statusRes.ok) {
        setLiveStatus(await statusRes.json());
      }
      if (irsRes.ok) {
        const irData = await irsRes.json();
        setIrsFiles(irData.files || []);
        setActiveIrs(irData.active || '');
      }
      if (presetsRes.ok) {
        setCustomPresets(await presetsRes.json());
      }
      if (eqPresetsRes.ok) {
        setCustomEqPresets(await eqPresetsRes.json());
      }
      setSyncStatus('connected');
    } catch {
      setSyncStatus('offline');
    }
  };

  const applyPresetByName = (name: string) => {
    setSelectedPresetName(name);
    const found = allPresets.find(p => p.name === name);
    if (found) {
      pushParams({ ...params, ...found.params }, true);
    }
  };

  const handleSavePreset = async () => {
    const name = window.prompt('Nhập tên Preset muốn lưu:', selectedPresetName || 'My Custom Preset');
    if (!name || !name.trim()) return;
    const presetObj: Preset = {
      name: name.trim(),
      subtitle: 'Custom saved preset',
      params: { ...params }
    };
    const res = await fetch('/api/presets/save', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(presetObj)
    });
    if (res.ok) {
      const data = await res.json();
      setCustomPresets(data.presets || []);
      setSelectedPresetName(data.savedName || presetObj.name);
    }
  };

  const handleBatchLoadPresets = async (files: FileList) => {
    const items: Preset[] = [];
    for (const file of Array.from(files)) {
      try {
        const text = await file.text();
        const parsed = JSON.parse(text);
        if (Array.isArray(parsed)) {
          for (const entry of parsed) {
            if (entry && typeof entry === 'object') {
              items.push({
                name: entry.name || file.name.replace(/\.json$/i, ''),
                subtitle: entry.subtitle || 'Imported preset',
                params: entry.params || entry
              });
            }
          }
        } else if (parsed && typeof parsed === 'object') {
          items.push({
            name: parsed.name || file.name.replace(/\.json$/i, ''),
            subtitle: parsed.subtitle || 'Imported preset',
            params: parsed.params || parsed
          });
        }
      } catch {
        // ignore invalid json file
      }
    }
    if (items.length === 0) return;

    const res = await fetch('/api/presets/import', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ items })
    });
    if (res.ok) {
      const data = await res.json();
      setCustomPresets(data.presets || []);
      if (data.imported && data.imported.length > 0) {
        const first = data.imported[0];
        setSelectedPresetName(first);
      }
    }
  };

  const applyEqPresetByName = (name: string) => {
    setSelectedEqPresetName(name);
    const found = allEqPresets.find(p => p.name === name);
    if (!found) return;
    pushParams(
      {
        ...params,
        eqEnabled: true,
        eqMode: found.eqMode ?? params.eqMode,
        eqBandsDb: found.eqBandsDb && found.eqBandsDb.length === 10 ? found.eqBandsDb : params.eqBandsDb,
        eqBands18Db:
          found.eqBands18Db && found.eqBands18Db.length === 18 ? found.eqBands18Db : params.eqBands18Db
      },
      true
    );
  };

  const handleSaveEqPreset = async () => {
    const name = window.prompt('Nhập tên EQ Preset muốn lưu:', selectedEqPresetName || 'My EQ');
    if (!name || !name.trim()) return;
    const eqObj: EqPreset = {
      name: name.trim(),
      eqMode: params.eqMode,
      eqBandsDb: [...params.eqBandsDb],
      eqBands18Db: [...params.eqBands18Db]
    };
    const res = await fetch('/api/eq-presets/save', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(eqObj)
    });
    if (res.ok) {
      const data = await res.json();
      setCustomEqPresets(data.presets || []);
      setSelectedEqPresetName(data.savedName || eqObj.name);
    }
  };

  const handleBatchLoadEqPresets = async (files: FileList) => {
    const items: EqPreset[] = [];
    for (const file of Array.from(files)) {
      try {
        const text = await file.text();
        const parsed = JSON.parse(text);
        const list = Array.isArray(parsed) ? parsed : [parsed];
        for (const entry of list) {
          if (entry && typeof entry === 'object') {
            items.push({
              name: entry.name || file.name.replace(/\.json$/i, ''),
              eqMode: typeof entry.eqMode === 'number' ? entry.eqMode : entry.eqBands18Db ? 1 : 0,
              eqBandsDb: entry.eqBandsDb,
              eqBands18Db: entry.eqBands18Db
            });
          }
        }
      } catch {
        // ignore invalid json
      }
    }
    if (items.length === 0) return;

    const res = await fetch('/api/eq-presets/import', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ items })
    });
    if (res.ok) {
      const data = await res.json();
      setCustomEqPresets(data.presets || []);
      if (data.imported && data.imported.length > 0) {
        setSelectedEqPresetName(data.imported[0]);
      }
    }
  };

  const handleIrsUpload = async (file: File) => {
    const reader = new FileReader();
    reader.onload = async () => {
      const dataUrl = String(reader.result || '');
      const base64 = dataUrl.includes(',') ? dataUrl.split(',')[1] : dataUrl;
      const res = await fetch('/api/irs/upload', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filename: file.name, base64 })
      });
      if (res.ok) {
        const data = await res.json();
        setIrsFiles(data.files || []);
        setActiveIrs(data.active || '');
        if (data.params) setParams({ ...DEFAULT_PARAMS, ...data.params });
      }
    };
    reader.readAsDataURL(file);
  };

  const handleIrsSelect = async (filename: string) => {
    if (!filename) return;
    const res = await fetch('/api/irs/select', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filename })
    });
    if (res.ok) {
      const data = await res.json();
      setActiveIrs(data.active || filename);
      if (data.params) setParams({ ...DEFAULT_PARAMS, ...data.params });
    }
  };

  useEffect(() => {
    void loadAll();
    const timer = window.setInterval(() => void loadStatus(), 800);
    return () => window.clearInterval(timer);
  }, []);

  const flushPendingParams = async () => {
    if (inFlightRef.current) return;
    inFlightRef.current = true;

    try {
      while (pendingParamsRef.current) {
        const toSend = pendingParamsRef.current;
        pendingParamsRef.current = null;
        setSyncStatus('syncing');
        const res = await fetch('/api/params', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(toSend)
        });
        setSyncStatus(res.ok ? 'connected' : 'offline');
      }
    } catch {
      setSyncStatus('offline');
    } finally {
      inFlightRef.current = false;
      if (pendingParamsRef.current) {
        void flushPendingParams();
      }
    }
  };

  const pushParams = (next: DspParams, _immediate = false) => {
    setParams(next);
    // Single-flight latest-only coalescing: never overlaps or replays stale steps
    pendingParamsRef.current = next;
    void flushPendingParams();
  };

  const updateField = <K extends keyof DspParams>(
    key: K,
    value: DspParams[K],
    immediate = false
  ) => {
    pushParams({ ...params, [key]: value }, immediate);
  };

  const updateEqBand = (index: number, db: number) => {
    if (params.eqMode === 1) {
      const nextBands = [...params.eqBands18Db];
      nextBands[index] = db;
      pushParams({ ...params, eqBands18Db: nextBands });
    } else {
      const nextBands = [...params.eqBandsDb];
      nextBands[index] = db;
      pushParams({ ...params, eqBandsDb: nextBands });
    }
  };

  const activeEqLabels = params.eqMode === 1 ? EQ_LABELS_18 : EQ_LABELS_10;
  const activeEqValues = params.eqMode === 1 ? params.eqBands18Db : params.eqBandsDb;

  const copyCommand = async (id: string, cmd: string) => {
    try {
      await navigator.clipboard.writeText(cmd);
      setCopiedCmd(id);
      setTimeout(() => setCopiedCmd(null), 1800);
    } catch {
      // ignore clipboard errors
    }
  };

  const defaultDevice = devices.find(d => d.isDefault) || devices[0];

  return (
    <div className="min-h-screen bg-black text-zinc-50">
      {/* Top Studio Header */}
      <header className="sticky top-0 z-20 border-b border-zinc-800/80 bg-black/90 backdrop-blur-md">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-5 py-3.5">
          <div className="flex items-center gap-3.5">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl border border-zinc-800 bg-zinc-950 shadow-inner">
              <AudioLines className="h-5 w-5 text-emerald-400" />
            </div>
            <div>
              <div className="flex items-center gap-2.5">
                <h1 className="font-display text-lg font-bold tracking-tight text-white">
                  ViPER4Windows RE
                </h1>
              </div>
              <p className="text-xs text-zinc-400">
                {defaultDevice
                  ? `${defaultDevice.name} · ${(defaultDevice.sampleRate / 1000).toFixed(1)} kHz / ${defaultDevice.bitsPerSample}-bit`
                  : 'Standalone Windows 11 Hi-Res Audio Engine'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {/* Navigation Tabs */}
            <nav className="flex rounded-lg border border-zinc-800 bg-zinc-950 p-1">
              <button
                type="button"
                onClick={() => setActiveTab('dsp')}
                className={`flex cursor-pointer items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
                  activeTab === 'dsp'
                    ? 'bg-zinc-800 text-white'
                    : 'text-zinc-400 hover:text-zinc-200'
                }`}
              >
                <Sliders className="h-3.5 w-3.5" />
                DSP Console
              </button>
              <button
                type="button"
                onClick={() => setActiveTab('devices')}
                className={`flex cursor-pointer items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
                  activeTab === 'devices'
                    ? 'bg-zinc-800 text-white'
                    : 'text-zinc-400 hover:text-zinc-200'
                }`}
              >
                <Headphones className="h-3.5 w-3.5" />
                Devices & APO
              </button>
            </nav>

            {/* Engine Working / Not Working Status Pill */}
            {(() => {
              const isWorking =
                params.masterEnabled &&
                syncStatus !== 'offline' &&
                liveStatus?.mode !== 'Exclusive Mode';
              return (
                <div
                  className={`flex items-center gap-2 rounded-lg border px-3 py-1.5 font-mono text-xs font-semibold tracking-wide transition-colors ${
                    isWorking
                      ? 'border-emerald-400/50 bg-emerald-500/20 text-emerald-200'
                      : 'border-zinc-800 bg-zinc-950 text-zinc-400'
                  }`}
                  title={
                    liveStatus?.mode === 'Exclusive Mode'
                      ? 'Bypassed by WASAPI Exclusive Mode'
                      : isWorking
                        ? 'ViPER4Windows RE DSP is actively processing audio'
                        : 'ViPER4Windows RE DSP is bypassed or offline'
                  }
                >
                  <span
                    className={`h-2 w-2 rounded-full ${
                      isWorking
                        ? 'bg-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.85)]'
                        : syncStatus === 'offline'
                          ? 'bg-red-500'
                          : 'bg-zinc-500'
                    }`}
                  />
                  <span>{isWorking ? 'WORKING' : 'NOT WORKING'}</span>
                </div>
              );
            })()}

            {/* Master Power Switch */}
            <button
              type="button"
              role="switch"
              aria-checked={params.masterEnabled}
              aria-label="Master Engine Power"
              title={params.masterEnabled ? 'Turn Off Master Engine' : 'Turn On Master Engine'}
              onClick={() => updateField('masterEnabled', !params.masterEnabled, true)}
              className={`relative inline-flex h-7 w-13 shrink-0 cursor-pointer items-center rounded-full border transition-colors duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white ${
                params.masterEnabled
                  ? 'border-emerald-400/60 bg-emerald-500/25'
                  : 'border-zinc-700 bg-zinc-900'
              }`}
            >
              <span
                className={`pointer-events-none flex h-5 w-5 items-center justify-center rounded-full transition-transform duration-200 ${
                  params.masterEnabled
                    ? 'translate-x-6.5 bg-emerald-400 text-black shadow-[0_0_10px_rgba(52,211,153,0.75)]'
                    : 'translate-x-1 bg-zinc-500 text-zinc-950'
                }`}
              >
                <Power className="h-3 w-3" />
              </span>
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-5 py-6">
        {/* Live Signal Path & Source Quality Monitor */}
        {liveStatus && (
          <section className="mb-6 flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-zinc-800/90 bg-gradient-to-r from-[#0c0c0c] via-[#0e1210] to-[#0c0c0c] px-5 py-3.5">
            <div className="flex min-w-0 items-center gap-3.5">
              <div
                className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border ${
                  liveStatus.zeroResample
                    ? 'border-emerald-500/40 bg-emerald-500/10 text-emerald-400'
                    : 'border-zinc-700 bg-zinc-900 text-zinc-300'
                }`}
              >
                <Activity className="h-4 w-4" />
              </div>
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-display text-xs font-bold uppercase tracking-wider text-zinc-200">
                    {liveStatus.source}
                  </span>
                  <span
                    className={`rounded px-2 py-0.5 font-mono text-[10px] font-semibold uppercase ${
                      liveStatus.zeroResample
                        ? 'border border-emerald-500/35 bg-emerald-500/10 text-emerald-300'
                        : 'bg-zinc-900 text-zinc-400'
                    }`}
                  >
                    {liveStatus.mode}
                  </span>
                  {liveStatus.extraApps && liveStatus.extraApps.length > 0 && (
                    <span className="rounded bg-zinc-900 px-2 py-0.5 font-mono text-[10px] text-zinc-400">
                      + {liveStatus.extraApps.join(', ')}
                    </span>
                  )}
                </div>
                <p className="mt-0.5 truncate text-xs text-zinc-400">
                  {liveStatus.title
                    ? `${liveStatus.artist ? `${liveStatus.artist} — ` : ''}${liveStatus.title}`
                    : liveStatus.deviceName}
                </p>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2 font-mono text-xs tabular-nums">
              {liveStatus.isHiRes && (
                <span className="rounded-lg border border-amber-400/40 bg-amber-500/15 px-2.5 py-1 text-[11px] font-semibold text-amber-300">
                  HI-RES AUDIO
                </span>
              )}
              {liveStatus.codec && (
                <span className="rounded-lg border border-zinc-800 bg-black px-2.5 py-1 text-zinc-200">
                  {liveStatus.codec}
                </span>
              )}
              <span className="rounded-lg border border-emerald-500/30 bg-black px-2.5 py-1 font-semibold text-emerald-300">
                {(liveStatus.sampleRate / 1000).toFixed(1)} kHz
              </span>
              {liveStatus.bitDepth > 0 && (
                <span className="rounded-lg border border-zinc-800 bg-black px-2.5 py-1 text-zinc-200">
                  {liveStatus.bitDepth}-bit
                </span>
              )}
              {liveStatus.bitrateKbps > 0 && (
                <span className="rounded-lg border border-zinc-800 bg-black px-2.5 py-1 text-zinc-400">
                  {liveStatus.bitrateKbps.toLocaleString()} kbps
                </span>
              )}
            </div>
          </section>
        )}

        {activeTab === 'dsp' ? (
          <div className="space-y-6">
            {/* Pinned Presets Strip */}
            {pinnedPresets.length > 0 && (
              <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                {pinnedPresets.map(preset => {
                  const isActive = selectedPresetName === preset.name;
                  return (
                    <div
                      key={preset.name}
                      role="button"
                      tabIndex={0}
                      onClick={() => applyPresetByName(preset.name)}
                      onKeyDown={e => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          applyPresetByName(preset.name);
                        }
                      }}
                      className={`group cursor-pointer rounded-xl border p-3.5 text-left transition-colors ${
                        isActive
                          ? 'border-emerald-500/50 bg-emerald-950/20'
                          : 'border-zinc-800/90 bg-[#0c0c0c] hover:border-zinc-600 hover:bg-zinc-900/70'
                      }`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="truncate font-display text-sm font-semibold text-zinc-100 group-hover:text-emerald-300">
                          {preset.name}
                        </span>
                        <button
                          type="button"
                          title="Unpin preset"
                          aria-label={`Unpin ${preset.name}`}
                          onClick={e => {
                            e.stopPropagation();
                            togglePinPreset(preset.name);
                          }}
                          className="inline-flex h-6 w-6 shrink-0 cursor-pointer items-center justify-center rounded-md text-emerald-400/80 transition-colors hover:bg-zinc-800 hover:text-amber-300"
                        >
                          <PinOff className="h-3.5 w-3.5" />
                        </button>
                      </div>
                      <p className="mt-1 truncate text-xs text-zinc-400">{preset.subtitle}</p>
                    </div>
                  );
                })}
              </section>
            )}

            {/* Full Preset Manager Bar (Dropdown + Pin + Save + Batch Load) */}
            <section className="flex flex-wrap items-center gap-2.5 rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-3">
              <select
                aria-label="Select Preset"
                value={selectedPresetName}
                onChange={e => applyPresetByName(e.target.value)}
                className="min-w-[220px] flex-1 cursor-pointer rounded-xl border border-zinc-800 bg-black px-3.5 py-2.5 font-display text-sm font-medium text-zinc-100 focus:border-emerald-500 focus:outline-none"
              >
                {allPresets.map(p => (
                  <option key={p.name} value={p.name}>
                    {p.name}
                  </option>
                ))}
              </select>

              <button
                type="button"
                onClick={() => togglePinPreset(selectedPresetName)}
                className={`flex cursor-pointer items-center gap-1.5 rounded-xl border px-4 py-2.5 text-xs font-semibold uppercase tracking-wide transition-colors ${
                  pinnedPresetNames.includes(selectedPresetName)
                    ? 'border-emerald-500/50 bg-emerald-500/15 text-emerald-300 hover:bg-emerald-500/25'
                    : 'border-zinc-800 bg-zinc-950 text-zinc-200 hover:border-zinc-600'
                }`}
              >
                <Pin className="h-3.5 w-3.5" />
                {pinnedPresetNames.includes(selectedPresetName) ? 'Pinned' : 'Pin'}
              </button>

              <button
                type="button"
                onClick={() => void handleSavePreset()}
                className="flex cursor-pointer items-center gap-1.5 rounded-xl border border-zinc-800 bg-zinc-950 px-4 py-2.5 text-xs font-semibold uppercase tracking-wide text-zinc-200 transition-colors hover:border-zinc-600 hover:text-white"
              >
                <Save className="h-3.5 w-3.5" />
                Save
              </button>

              <label className="flex cursor-pointer items-center gap-1.5 rounded-xl border border-zinc-800 bg-zinc-950 px-4 py-2.5 text-xs font-semibold uppercase tracking-wide text-zinc-200 transition-colors hover:border-emerald-500/60 hover:text-emerald-300">
                <FolderOpen className="h-3.5 w-3.5" />
                Load
                <input
                  type="file"
                  accept=".json"
                  multiple
                  className="hidden"
                  onChange={e => {
                    if (e.target.files && e.target.files.length > 0) {
                      void handleBatchLoadPresets(e.target.files);
                    }
                    e.target.value = '';
                  }}
                />
              </label>
            </section>

            {/* Multi-Band Hi-Res Parametric Equalizer (10-Band / 18-Band) */}
            <section className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                <div className="flex flex-wrap items-center gap-2.5">
                  <Activity className="h-4 w-4 text-emerald-400" />
                  <h2 className="font-display text-base font-semibold text-white">
                    {params.eqMode === 1 ? '18-Band' : '10-Band'} Hi-Res Equalizer
                  </h2>
                  <InfoTip
                    align="left"
                    text="Bộ cân bằng 10-Band hoặc 18-Band dùng bộ lọc Biquad 64-bit. Chuyển sang 18-Band để tinh chỉnh chi tiết từng nửa quãng tám giống ViPER4Windows gốc."
                  />
                  {/* EQ Band Mode Switch */}
                  <div className="flex rounded-lg border border-zinc-800 bg-black p-0.5">
                    <button
                      type="button"
                      onClick={() => updateField('eqMode', 0, true)}
                      className={`cursor-pointer rounded-md px-2.5 py-1 font-mono text-[11px] font-medium transition-colors ${
                        params.eqMode === 0
                          ? 'bg-zinc-800 text-emerald-300'
                          : 'text-zinc-400 hover:text-zinc-200'
                      }`}
                    >
                      10-Band
                    </button>
                    <button
                      type="button"
                      onClick={() => updateField('eqMode', 1, true)}
                      className={`cursor-pointer rounded-md px-2.5 py-1 font-mono text-[11px] font-medium transition-colors ${
                        params.eqMode === 1
                          ? 'bg-zinc-800 text-emerald-300'
                          : 'text-zinc-400 hover:text-zinc-200'
                      }`}
                    >
                      18-Band
                    </button>
                  </div>
                </div>

                {/* EQ Preset Dropdown + Save + Load + Reset + Toggle */}
                <div className="flex flex-wrap items-center gap-2">
                  <select
                    aria-label="EQ Preset"
                    value={selectedEqPresetName}
                    onChange={e => applyEqPresetByName(e.target.value)}
                    className="w-40 cursor-pointer rounded-lg border border-zinc-800 bg-black px-2.5 py-1.5 text-xs text-zinc-200 focus:border-emerald-500 focus:outline-none sm:w-48"
                  >
                    {allEqPresets.map(ep => (
                      <option key={ep.name} value={ep.name}>
                        {ep.name}
                      </option>
                    ))}
                  </select>

                  <button
                    type="button"
                    onClick={() => void handleSaveEqPreset()}
                    className="flex cursor-pointer items-center gap-1 rounded-lg border border-zinc-800 bg-zinc-950 px-2.5 py-1.5 text-xs text-zinc-300 transition-colors hover:border-zinc-600 hover:text-white"
                  >
                    <Save className="h-3 w-3" />
                    Save
                  </button>

                  <label className="flex cursor-pointer items-center gap-1 rounded-lg border border-zinc-800 bg-zinc-950 px-2.5 py-1.5 text-xs text-zinc-300 transition-colors hover:border-emerald-500/60 hover:text-emerald-300">
                    <FolderOpen className="h-3 w-3" />
                    Load
                    <input
                      type="file"
                      accept=".json"
                      multiple
                      className="hidden"
                      onChange={e => {
                        if (e.target.files && e.target.files.length > 0) {
                          void handleBatchLoadEqPresets(e.target.files);
                        }
                        e.target.value = '';
                      }}
                    />
                  </label>

                  <button
                    type="button"
                    onClick={() => {
                      if (params.eqMode === 1) {
                        pushParams(
                          { ...params, eqBands18Db: Array(18).fill(0) },
                          true
                        );
                      } else {
                        pushParams(
                          { ...params, eqBandsDb: Array(10).fill(0) },
                          true
                        );
                      }
                    }}
                    className="flex cursor-pointer items-center gap-1.5 rounded-lg border border-zinc-800 bg-zinc-950 px-2.5 py-1.5 text-xs text-zinc-300 transition-colors hover:border-zinc-700 hover:text-white"
                  >
                    <RotateCcw className="h-3 w-3" />
                    Reset EQ
                  </button>

                  <Toggle
                    checked={params.eqEnabled}
                    onChange={v => updateField('eqEnabled', v, true)}
                    label="Toggle Equalizer"
                  />
                </div>
              </div>

              <EqCurveSvg
                bands={activeEqValues}
                enabled={params.eqEnabled && params.masterEnabled}
              />

              <div
                className={`mt-5 grid gap-1.5 ${
                  params.eqMode === 1
                    ? 'grid-cols-6 sm:grid-cols-9 lg:grid-cols-18'
                    : 'grid-cols-5 sm:grid-cols-10'
                }`}
              >
                {activeEqLabels.map((freq, idx) => {
                  const val = activeEqValues[idx] ?? 0;
                  return (
                    <div key={`${params.eqMode}-${freq}`} className="flex flex-col items-center gap-2">
                      <span className="font-mono text-[10px] tabular-nums text-zinc-300">
                        {val > 0 ? `+${val.toFixed(1)}` : val.toFixed(1)}
                      </span>
                      <input
                        type="range"
                        min={-12}
                        max={12}
                        step={0.5}
                        value={val}
                        aria-label={`${freq} Hz gain`}
                        onChange={e => updateEqBand(idx, parseFloat(e.target.value))}
                        className="eq-fader"
                      />
                      <span className="font-mono text-[10px] text-zinc-500">{freq}</span>
                    </div>
                  );
                })}
              </div>
            </section>

            {/* DSP Modules Grid */}
            <section className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
              {/* ViPER Bass */}
              <div
                style={{ order: getModuleOrder('bass', 0) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Waves className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">ViPER Bass</h3>
                    <InfoTip
                      align="left"
                      text="Tăng cường dải siêu trầm sâu và có lực. Natural: tăng trầm tự nhiên; PureBass+: trầm gọn, chắc có hài bậc 2; Subwoofer: tái tạo lực rung sâu như loa sub."
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('bass')}
                    <Toggle
                      checked={params.bassEnabled}
                      onChange={v => updateField('bassEnabled', v, true)}
                      label="Toggle ViPER Bass"
                    />
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="grid grid-cols-3 gap-1.5 rounded-lg border border-zinc-800 bg-black p-1">
                    {['Natural', 'PureBass+', 'Subwoofer'].map((label, idx) => (
                      <button
                        key={label}
                        type="button"
                        onClick={() => updateField('bassMode', idx, true)}
                        className={`cursor-pointer rounded-md py-1.5 text-[11px] font-medium transition-colors ${
                          params.bassMode === idx
                            ? 'bg-zinc-800 text-white'
                            : 'text-zinc-400 hover:text-zinc-200'
                        }`}
                      >
                        {label}
                      </button>
                    ))}
                  </div>

                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Bass Frequency
                        <InfoTip
                          align="left"
                          text="Tần số cắt của dải trầm (30–120Hz). Mức 50–65Hz tập trung vào sub-bass sâu, mức 80–100Hz tăng lực trống mid-bass dày hơn."
                        />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {params.bassFrequencyHz.toFixed(0)} Hz
                      </span>
                    </div>
                    <input
                      type="range"
                      min={30}
                      max={120}
                      step={5}
                      value={params.bassFrequencyHz}
                      onChange={e => updateField('bassFrequencyHz', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>

                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Bass Boost
                        <InfoTip
                          align="left"
                          text="Độ lớn tăng cường âm trầm (0 đến +15 dB). Nên bắt đầu ở mức +3.5 đến +6 dB để giữ độ sạch cho dải trung."
                        />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        +{params.bassGainDb.toFixed(1)} dB
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={15}
                      step={0.5}
                      value={params.bassGainDb}
                      onChange={e => updateField('bassGainDb', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>
                </div>
              </div>

              {/* ViPER Clarity */}
              <div
                style={{ order: getModuleOrder('clarity', 1) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Sparkles className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">ViPER Clarity</h3>
                    <InfoTip text="Tăng độ trong trẻo và tách bạch chi tiết nhạc cụ/giọng hát. Natural: sáng mượt tự nhiên; OZone+: thêm độ thoáng (air) dải cao; XHiFi: tái tạo chi tiết sắc nét cho tai nghe Hi-Res." />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('clarity')}
                    <Toggle
                      checked={params.clarityEnabled}
                      onChange={v => updateField('clarityEnabled', v, true)}
                      label="Toggle ViPER Clarity"
                    />
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="grid grid-cols-3 gap-1.5 rounded-lg border border-zinc-800 bg-black p-1">
                    {['Natural', 'OZone+', 'XHiFi'].map((label, idx) => (
                      <button
                        key={label}
                        type="button"
                        onClick={() => updateField('clarityMode', idx, true)}
                        className={`cursor-pointer rounded-md py-1.5 text-[11px] font-medium transition-colors ${
                          params.clarityMode === idx
                            ? 'bg-zinc-800 text-white'
                            : 'text-zinc-400 hover:text-zinc-200'
                        }`}
                      >
                        {label}
                      </button>
                    ))}
                  </div>

                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Clarity Level
                        <InfoTip text="Mức độ nâng chi tiết dải cao (0 đến +15 dB). Mức +2 đến +4 dB giúp giọng hát và nhạc cụ nổi khối mà không bị chói gắt." />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        +{params.clarityGainDb.toFixed(1)} dB
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={15}
                      step={0.5}
                      value={params.clarityGainDb}
                      onChange={e => updateField('clarityGainDb', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>
                </div>
              </div>

              {/* Tube Sound Simulator */}
              <div
                style={{ order: getModuleOrder('tube', 2) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Flame className="h-4 w-4 text-amber-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Tube Simulator (6N1J)
                    </h3>
                    <InfoTip
                      align="right"
                      text="Giả lập chất âm ấm áp của ampli đèn điện tử 6N1J bằng cách tạo hài âm bậc chẵn nhẹ nhàng, làm mềm các dải cao gắt và dày giọng hát."
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('tube')}
                    <Toggle
                      checked={params.tubeEnabled}
                      onChange={v => updateField('tubeEnabled', v, true)}
                      label="Toggle Tube Simulator"
                    />
                  </div>
                </div>

                <p className="mb-4 text-xs text-zinc-400">
                  Asymmetric even-order harmonic warmth with automatic sample-rate-invariant DC blocker.
                </p>

                <div>
                  <div className="mb-1.5 flex justify-between text-xs">
                    <span className="inline-flex items-center gap-1.5 text-zinc-400">
                      Warmth / Drive
                      <InfoTip
                        align="right"
                        text="Cường độ giả lập đèn (0–100%). Mức 20–40% cho độ ấm analog mượt mà; cao hơn sẽ nghe rõ độ bão hòa cổ điển (vintage)."
                      />
                    </span>
                    <span className="font-mono tabular-nums text-zinc-200">
                      {Math.round(params.tubeDrive * 100)}%
                    </span>
                  </div>
                  <input
                    type="range"
                    min={0}
                    max={1}
                    step={0.02}
                    value={params.tubeDrive}
                    onChange={e => updateField('tubeDrive', parseFloat(e.target.value))}
                    className="studio-slider w-full"
                  />
                </div>
              </div>

              {/* Differential Surround & Stereo Field */}
              <div
                style={{ order: getModuleOrder('surround', 3) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Headphones className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Differential Surround
                    </h3>
                    <InfoTip
                      align="left"
                      text="Mở rộng không gian âm trường (soundstage) bằng hiệu ứng trễ Haas và tách kênh Mid/Side, giúp âm thanh rộng mở ra ngoài đầu khi đeo tai nghe."
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('surround')}
                    <Toggle
                      checked={params.surroundEnabled}
                      onChange={v => updateField('surroundEnabled', v, true)}
                      label="Toggle Differential Surround"
                    />
                  </div>
                </div>

                <div className="space-y-4">
                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Haas Delay
                        <InfoTip
                          align="left"
                          text="Độ trễ nhẹ giữa 2 kênh trái/phải (0–20 ms). Mức 2–6 ms tạo cảm giác không gian rộng tự nhiên; trên 12 ms tạo hiệu ứng vòm rõ rệt."
                        />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {params.surroundDelayMs.toFixed(1)} ms
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={20}
                      step={0.5}
                      value={params.surroundDelayMs}
                      onChange={e => updateField('surroundDelayMs', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>

                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Stereo Soundstage
                        <InfoTip
                          align="left"
                          text="Độ rộng âm trường Mid/Side. 100% là nguyên bản, dưới 100% thu hẹp về Mono, trên 100% (115–140%) đẩy nhạc cụ sang hai bên rộng hơn."
                        />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {Math.round(params.stereoWidth * 100)}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={2}
                      step={0.05}
                      value={params.stereoWidth}
                      onChange={e => updateField('stereoWidth', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>
                </div>
              </div>

              {/* Master Output & Pan */}
              <div
                style={{ order: getModuleOrder('master', 4) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Volume2 className="h-4 w-4 text-zinc-200" />
                    <h3 className="font-display text-sm font-semibold text-white">Master Output</h3>
                    <InfoTip text="Điều chỉnh tổng âm lượng đầu ra (Pre-amp) và độ cân bằng giữa 2 bên tai nghe Trái / Phải." />
                  </div>
                  {renderModulePin('master')}
                </div>

                <div className="space-y-4">
                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Output Gain
                        <InfoTip text="Tăng hoặc giảm tổng độ lớn tín hiệu (-24 đến +12 dB). Nếu tăng EQ hoặc Bass nhiều, có thể giảm nhẹ -2 đến -4 dB để tạo khoảng thở (headroom)." />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {params.masterGainDb > 0 ? `+${params.masterGainDb.toFixed(1)}` : params.masterGainDb.toFixed(1)} dB
                      </span>
                    </div>
                    <input
                      type="range"
                      min={-24}
                      max={12}
                      step={0.5}
                      value={params.masterGainDb}
                      onChange={e => updateField('masterGainDb', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>

                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        L / R Balance
                        <InfoTip text="Cân bằng âm lượng giữa kênh Trái (L) và Phải (R). Hữu ích nếu một bên tai nghe bị lệch âm lượng nhẹ." />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {params.outputPan === 0
                          ? 'Center'
                          : params.outputPan < 0
                            ? `L ${Math.round(Math.abs(params.outputPan) * 100)}%`
                            : `R ${Math.round(params.outputPan * 100)}%`}
                      </span>
                    </div>
                    <input
                      type="range"
                      min={-1}
                      max={1}
                      step={0.05}
                      value={params.outputPan}
                      onChange={e => updateField('outputPan', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>
                </div>
              </div>

              {/* Brickwall Peak Limiter */}
              <div
                style={{ order: getModuleOrder('limiter', 5) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <ShieldCheck className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Peak Limiter
                    </h3>
                    <InfoTip
                      align="right"
                      text="Bộ giới hạn đỉnh âm lượng (nên luôn bật). Tự động ghìm các đỉnh tín hiệu vượt ngưỡng 0 dBFS để chống rè và vỡ tiếng kỹ thuật số."
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('limiter')}
                    <Toggle
                      checked={params.limiterEnabled}
                      onChange={v => updateField('limiterEnabled', v, true)}
                      label="Toggle Peak Limiter"
                    />
                  </div>
                </div>

                <p className="mb-4 text-xs text-zinc-400">
                  Prevents digital clipping above 0 dBFS when stacking heavy Bass & EQ boosts.
                </p>

                <div>
                  <div className="mb-1.5 flex justify-between text-xs">
                    <span className="inline-flex items-center gap-1.5 text-zinc-400">
                      Ceiling Threshold
                      <InfoTip
                        align="right"
                        text="Ngưỡng trần tối đa cho phép (-6.0 đến 0.0 dBFS). Mức -0.3 dBFS là chuẩn phòng thu giúp chống méo tiếng trên DAC."
                      />
                    </span>
                    <span className="font-mono tabular-nums text-zinc-200">
                      {params.limiterThresholdDb.toFixed(1)} dBFS
                    </span>
                  </div>
                  <input
                    type="range"
                    min={-6}
                    max={0}
                    step={0.1}
                    value={params.limiterThresholdDb}
                    onChange={e => updateField('limiterThresholdDb', parseFloat(e.target.value))}
                    className="studio-slider w-full"
                  />
                </div>
              </div>

              {/* Dynamic System */}
              <div
                style={{ order: getModuleOrder('dynamic', 6) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Zap className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Dynamic System
                    </h3>
                    <InfoTip
                      align="left"
                      text="Tự động đo nhịp đánh (transient) của bản nhạc để tăng lực nảy của trống và độ sống động theo thời gian thực, chọn đúng loại tai nghe/loa để đạt độ cắt tần hợp lý nhất."
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('dynamic')}
                    <Toggle
                      checked={params.dynamicEnabled}
                      onChange={v => updateField('dynamicEnabled', v, true)}
                      label="Toggle Dynamic System"
                    />
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="grid grid-cols-2 gap-1.5 rounded-lg border border-zinc-800 bg-black p-1">
                    {['Common', 'Extreme HP', 'High-End HP', 'Studio'].map((label, idx) => (
                      <button
                        key={label}
                        type="button"
                        onClick={() => updateField('dynamicDevice', idx, true)}
                        className={`cursor-pointer rounded-md py-1.5 text-[11px] font-medium transition-colors ${
                          params.dynamicDevice === idx
                            ? 'bg-zinc-800 text-white'
                            : 'text-zinc-400 hover:text-zinc-200'
                        }`}
                      >
                        {label}
                      </button>
                    ))}
                  </div>

                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Dynamic Bass
                        <InfoTip
                          align="left"
                          text="Cường độ lực đánh động (0–100%). Mức 20–35% giúp nhạc EDM/Pop có độ nảy và lực hơn mà không bị ù nền."
                        />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {Math.round(params.dynamicBass)}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={100}
                      step={2}
                      value={params.dynamicBass}
                      onChange={e => updateField('dynamicBass', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>
                </div>
              </div>

              {/* Reverberation (Room Simulator) */}
              <div
                style={{ order: getModuleOrder('reverb', 7) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Radio className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Reverberation
                    </h3>
                    <InfoTip text="Giả lập tiếng vang phòng nghe / khán phòng giúp âm thanh có chiều sâu không gian như đang nghe dàn loa thật." />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('reverb')}
                    <Toggle
                      checked={params.reverbEnabled}
                      onChange={v => updateField('reverbEnabled', v, true)}
                      label="Toggle Reverberation"
                    />
                  </div>
                </div>

                <div className="space-y-3">
                  <div>
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Room Size
                        <InfoTip text="Kích thước phòng giả lập (0–100%). Thấp = phòng thu nhỏ, Cao = khán phòng rộng với đuôi vang dài hơn." />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {Math.round(params.reverbRoomSize * 100)}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={1}
                      step={0.02}
                      value={params.reverbRoomSize}
                      onChange={e => updateField('reverbRoomSize', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>

                  <div>
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Sound Field / Width
                        <InfoTip text="Độ lan tỏa không gian stereo của tiếng vang giữa hai kênh trái và phải." />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {Math.round(params.reverbWidth * 100)}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={1}
                      step={0.02}
                      value={params.reverbWidth}
                      onChange={e => updateField('reverbWidth', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>

                  <div>
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Damping
                        <InfoTip text="Độ tiêu âm dải cao của tường phòng. Tăng Damping làm tiếng vang ấm và mềm hơn, giảm tiếng vang kim loại chói." />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {Math.round(params.reverbDamping * 100)}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={1}
                      step={0.02}
                      value={params.reverbDamping}
                      onChange={e => updateField('reverbDamping', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <div className="mb-1 flex justify-between text-xs">
                        <span className="inline-flex items-center gap-1 text-zinc-400">
                          Wet
                          <InfoTip text="Tỷ lệ âm lượng của tiếng vang phòng trộn vào nhạc (nên để 10–25% cho tự nhiên)." />
                        </span>
                        <span className="font-mono tabular-nums text-zinc-200">
                          {Math.round(params.reverbWet * 100)}%
                        </span>
                      </div>
                      <input
                        type="range"
                        min={0}
                        max={1}
                        step={0.02}
                        value={params.reverbWet}
                        onChange={e => updateField('reverbWet', parseFloat(e.target.value))}
                        className="studio-slider w-full"
                      />
                    </div>
                    <div>
                      <div className="mb-1 flex justify-between text-xs">
                        <span className="inline-flex items-center gap-1 text-zinc-400">
                          Dry
                          <InfoTip text="Tỷ lệ âm thanh gốc chưa qua tiếng vang (thường giữ ở mức 80–100%)." />
                        </span>
                        <span className="font-mono tabular-nums text-zinc-200">
                          {Math.round(params.reverbDry * 100)}%
                        </span>
                      </div>
                      <input
                        type="range"
                        min={0}
                        max={1}
                        step={0.02}
                        value={params.reverbDry}
                        onChange={e => updateField('reverbDry', parseFloat(e.target.value))}
                        className="studio-slider w-full"
                      />
                    </div>
                  </div>
                </div>
              </div>

              {/* Compressor / Playback Gain Control */}
              <div
                style={{ order: getModuleOrder('compressor', 8) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Gauge className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Compressor / AGC
                    </h3>
                    <InfoTip
                      align="right"
                      text="Nén dải động thông minh (Soft-Knee): ghìm bớt các đoạn quá to và nâng các đoạn nhỏ lên đều nhau — cực kỳ hữu ích khi xem phim, podcast hoặc nghe nhạc ban đêm."
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('compressor')}
                    <Toggle
                      checked={params.compEnabled}
                      onChange={v => updateField('compEnabled', v, true)}
                      label="Toggle Compressor"
                    />
                  </div>
                </div>

                <div className="space-y-3">
                  <div>
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Threshold
                        <InfoTip
                          align="right"
                          text="Ngưỡng âm lượng bắt đầu nén (-40 đến 0 dB). Âm thanh vượt ngưỡng này sẽ bị nén xuống theo tỷ lệ Ratio."
                        />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {params.compThresholdDb.toFixed(0)} dB
                      </span>
                    </div>
                    <input
                      type="range"
                      min={-40}
                      max={0}
                      step={1}
                      value={params.compThresholdDb}
                      onChange={e => updateField('compThresholdDb', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>

                  <div>
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Ratio
                        <InfoTip
                          align="right"
                          text="Tỷ lệ nén (1:1 đến 20:1). Mức 2:1–4:1 nén êm tự nhiên cho nhạc; mức 6:1 trở lên nén mạnh để giữ âm lượng đều tăm tắp."
                        />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        {params.compRatio.toFixed(1)} : 1
                      </span>
                    </div>
                    <input
                      type="range"
                      min={1}
                      max={20}
                      step={0.5}
                      value={params.compRatio}
                      onChange={e => updateField('compRatio', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <div className="mb-1 flex justify-between text-xs">
                        <span className="inline-flex items-center gap-1 text-zinc-400">
                          Attack
                          <InfoTip text="Tốc độ bắt đầu nén khi có tiếng lớn đột ngột (1–100 ms). Thấp = phản ứng nhanh; Cao = giữ lại độ nảy ban đầu của tiếng trống." />
                        </span>
                        <span className="font-mono tabular-nums text-zinc-200">
                          {params.compAttackMs.toFixed(0)} ms
                        </span>
                      </div>
                      <input
                        type="range"
                        min={1}
                        max={100}
                        step={1}
                        value={params.compAttackMs}
                        onChange={e => updateField('compAttackMs', parseFloat(e.target.value))}
                        className="studio-slider w-full"
                      />
                    </div>
                    <div>
                      <div className="mb-1 flex justify-between text-xs">
                        <span className="inline-flex items-center gap-1 text-zinc-400">
                          Release
                          <InfoTip
                            align="right"
                            text="Thời gian nhả nén sau khi âm thanh giảm dưới ngưỡng (20–500 ms). Mức 100–200 ms giúp âm lượng hồi lại mượt mà."
                          />
                        </span>
                        <span className="font-mono tabular-nums text-zinc-200">
                          {params.compReleaseMs.toFixed(0)} ms
                        </span>
                      </div>
                      <input
                        type="range"
                        min={20}
                        max={500}
                        step={10}
                        value={params.compReleaseMs}
                        onChange={e => updateField('compReleaseMs', parseFloat(e.target.value))}
                        className="studio-slider w-full"
                      />
                    </div>
                  </div>

                  <div>
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="inline-flex items-center gap-1.5 text-zinc-400">
                        Makeup Gain
                        <InfoTip
                          align="right"
                          text="Bù lại âm lượng sau khi nén (0 đến +18 dB) giúp tổng thể bản nhạc nghe đầy đặn và rõ chi tiết nhỏ hơn."
                        />
                      </span>
                      <span className="font-mono tabular-nums text-zinc-200">
                        +{params.compMakeupDb.toFixed(1)} dB
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0}
                      max={18}
                      step={0.5}
                      value={params.compMakeupDb}
                      onChange={e => updateField('compMakeupDb', parseFloat(e.target.value))}
                      className="studio-slider w-full"
                    />
                  </div>
                </div>
              </div>

              {/* Cure Tech+ (Binaural Crossfeed) */}
              <div
                style={{ order: getModuleOrder('cure', 9) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Ear className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Cure Tech+ (Crossfeed)
                    </h3>
                    <InfoTip
                      align="left"
                      text="Công nghệ chống mệt tai khi đeo tai nghe lâu: trộn nhẹ một phần âm trầm đã trễ 0.26 ms từ tai này sang tai kia giống cách tai người nghe loa ngoài tự nhiên."
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('cure')}
                    <Toggle
                      checked={params.cureEnabled}
                      onChange={v => updateField('cureEnabled', v, true)}
                      label="Toggle Cure Tech+"
                    />
                  </div>
                </div>

                <p className="mb-4 text-xs text-zinc-400">
                  Binaural speaker crossfeed with 0.26 ms ITD to reduce headphone ear fatigue during long sessions.
                </p>

                <div className="grid grid-cols-3 gap-1.5 rounded-lg border border-zinc-800 bg-black p-1">
                  {['Slight', 'Moderate', 'Extreme'].map((label, idx) => (
                    <button
                      key={label}
                      type="button"
                      onClick={() => updateField('cureLevel', idx, true)}
                      className={`cursor-pointer rounded-md py-1.5 text-[11px] font-medium transition-colors ${
                        params.cureLevel === idx
                          ? 'bg-zinc-800 text-white'
                          : 'text-zinc-400 hover:text-zinc-200'
                      }`}
                    >
                      {label}
                    </button>
                  ))}
                </div>
              </div>

              {/* Convolver (.irs / .wav Impulse Response) */}
              <div
                style={{ order: getModuleOrder('convolver', 10) }}
                className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5 md:col-span-2"
              >
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <AudioLines className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Convolver (.irs / .wav)
                    </h3>
                    <InfoTip text="Tích chập (Convolution) mô phỏng chính xác đặc tính âm thanh của các bộ EQ, DAC, ampli đèn hoặc phòng thu nổi tiếng thông qua file xung mẫu .irs / .wav của cộng đồng ViPER." />
                  </div>
                  <div className="flex items-center gap-2">
                    {renderModulePin('convolver')}
                    <Toggle
                      checked={params.convolverEnabled}
                      onChange={v => updateField('convolverEnabled', v, true)}
                      label="Toggle Convolver"
                    />
                  </div>
                </div>

                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-3">
                    <label className="inline-flex items-center gap-1.5 text-xs text-zinc-400">
                      Impulse Response File
                      <InfoTip text="Chọn file .irs hoặc .wav từ máy. Hệ thống tự động resample lên đúng tần số DAC (96/192/384 kHz) để không bị lệch màu âm." />
                    </label>
                    <div className="flex gap-2">
                      <select
                        value={activeIrs}
                        onChange={e => void handleIrsSelect(e.target.value)}
                        className="min-w-0 flex-1 rounded-lg border border-zinc-800 bg-black px-3 py-2 text-xs text-zinc-200 focus:border-emerald-500 focus:outline-none"
                      >
                        {irsFiles.length === 0 ? (
                          <option value="">No .irs / .wav loaded yet</option>
                        ) : (
                          irsFiles.map(f => (
                            <option key={f} value={f}>
                              {f}
                            </option>
                          ))
                        )}
                      </select>

                      <label className="flex shrink-0 cursor-pointer items-center gap-1.5 rounded-lg border border-zinc-700 bg-zinc-900 px-3 py-2 text-xs font-medium text-zinc-100 transition-colors hover:border-emerald-500/60 hover:text-emerald-300">
                        <Upload className="h-3.5 w-3.5" />
                        Load .irs
                        <input
                          type="file"
                          accept=".irs,.wav"
                          className="hidden"
                          onChange={e => {
                            const f = e.target.files?.[0];
                            if (f) void handleIrsUpload(f);
                            e.target.value = '';
                          }}
                        />
                      </label>
                    </div>
                    <p className="text-[11px] text-zinc-500">
                      Automatically resamples 44.1/48 kHz .irs files to match your DAC (up to 384 kHz).
                    </p>
                  </div>

                  <div className="space-y-3">
                    <div>
                      <div className="mb-1.5 flex justify-between text-xs">
                        <span className="inline-flex items-center gap-1.5 text-zinc-400">
                          Cross-Channel
                          <InfoTip
                            align="right"
                            text="Độ hòa trộn chéo giữa 2 kênh Trái/Phải sau khi qua Convolver (0–100%). 0% giữ nguyên độ tách kênh gốc; 15–30% giúp âm hình tự nhiên hơn."
                          />
                        </span>
                        <span className="font-mono tabular-nums text-zinc-200">
                          {Math.round(params.convolverCrossChannel * 100)}%
                        </span>
                      </div>
                      <input
                        type="range"
                        min={0}
                        max={1}
                        step={0.02}
                        value={params.convolverCrossChannel}
                        onChange={e =>
                          updateField('convolverCrossChannel', parseFloat(e.target.value))
                        }
                        className="studio-slider w-full"
                      />
                    </div>

                    <div>
                      <div className="mb-1.5 flex justify-between text-xs">
                        <span className="inline-flex items-center gap-1.5 text-zinc-400">
                          Effect Mix (Wet)
                          <InfoTip
                            align="right"
                            text="Độ đậm nhạt của hiệu ứng file .irs (0–100%). 100% áp dụng toàn bộ file xung; giảm xuống 40–70% nếu muốn pha nhẹ với chất âm gốc."
                          />
                        </span>
                        <span className="font-mono tabular-nums text-zinc-200">
                          {Math.round(params.convolverWetMix * 100)}%
                        </span>
                      </div>
                      <input
                        type="range"
                        min={0}
                        max={1}
                        step={0.02}
                        value={params.convolverWetMix}
                        onChange={e => updateField('convolverWetMix', parseFloat(e.target.value))}
                        className="studio-slider w-full"
                      />
                    </div>
                  </div>
                </div>
              </div>
            </section>
          </div>
        ) : (
          /* Devices & APO Configurator Tab */
          <section className="space-y-5">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="font-display text-lg font-semibold text-white">
                  Playback Devices & Windows APO
                </h2>
                <p className="text-xs text-zinc-400">
                  Auto-backups factory Registry FxProperties before attaching ViPERHiResAPO.dll (SFX Hi-Res).
                </p>
              </div>
              <button
                type="button"
                onClick={() => void loadAll()}
                className="flex cursor-pointer items-center gap-1.5 rounded-lg border border-zinc-800 bg-zinc-950 px-3 py-2 text-xs font-medium text-zinc-200 hover:border-zinc-700"
              >
                <RefreshCw className="h-3.5 w-3.5" />
                Refresh Devices
              </button>
            </div>

            <div className="grid gap-4">
              {devices.map(dev => {
                const installCmd = `powershell -ExecutionPolicy Bypass -File .\\scripts\\install_apo.ps1 -DeviceGuid "${dev.guid}"`;
                const uninstallCmd = `powershell -ExecutionPolicy Bypass -File .\\scripts\\uninstall_apo.ps1 -DeviceGuid "${dev.guid}"`;
                return (
                  <div
                    key={dev.id}
                    className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div className="flex items-center gap-3">
                        <div className="flex h-10 w-10 items-center justify-center rounded-xl border border-zinc-800 bg-zinc-950">
                          <Headphones className="h-5 w-5 text-emerald-400" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2">
                            <h3 className="font-display text-sm font-semibold text-white">
                              {dev.name}
                            </h3>
                            {dev.isDefault && (
                              <span className="rounded bg-emerald-500/15 px-2 py-0.5 text-[10px] font-semibold text-emerald-300">
                                DEFAULT
                              </span>
                            )}
                          </div>
                          <p className="font-mono text-xs text-zinc-400">
                            GUID: {dev.guid} · {(dev.sampleRate / 1000).toFixed(1)} kHz · {dev.channels}ch · {dev.bitsPerSample}-bit
                          </p>
                        </div>
                      </div>
                    </div>

                    <div className="mt-4 grid gap-3 md:grid-cols-2">
                      <div className="rounded-xl border border-zinc-800 bg-black p-3">
                        <div className="mb-1.5 flex items-center justify-between">
                          <span className="text-[11px] font-semibold text-emerald-300">
                            Install Hi-Res APO (Run as Admin)
                          </span>
                          <button
                            type="button"
                            onClick={() => void copyCommand(`in-${dev.guid}`, installCmd)}
                            className="flex cursor-pointer items-center gap-1 text-[11px] text-zinc-400 hover:text-white"
                          >
                            {copiedCmd === `in-${dev.guid}` ? (
                              <>
                                <Check className="h-3 w-3 text-emerald-400" /> Copied
                              </>
                            ) : (
                              <>
                                <Copy className="h-3 w-3" /> Copy
                              </>
                            )}
                          </button>
                        </div>
                        <code className="block break-all font-mono text-[11px] text-zinc-300">
                          {installCmd}
                        </code>
                      </div>

                      <div className="rounded-xl border border-zinc-800 bg-black p-3">
                        <div className="mb-1.5 flex items-center justify-between">
                          <span className="text-[11px] font-semibold text-amber-300">
                            1-Click Restore Factory Audio (Run as Admin)
                          </span>
                          <button
                            type="button"
                            onClick={() => void copyCommand(`un-${dev.guid}`, uninstallCmd)}
                            className="flex cursor-pointer items-center gap-1 text-[11px] text-zinc-400 hover:text-white"
                          >
                            {copiedCmd === `un-${dev.guid}` ? (
                              <>
                                <Check className="h-3 w-3 text-emerald-400" /> Copied
                              </>
                            ) : (
                              <>
                                <Copy className="h-3 w-3" /> Copy
                              </>
                            )}
                          </button>
                        </div>
                        <code className="block break-all font-mono text-[11px] text-zinc-300">
                          {uninstallCmd}
                        </code>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </section>
        )}
      </main>
    </div>
  );
}
