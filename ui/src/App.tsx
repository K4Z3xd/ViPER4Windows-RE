import React, { useEffect, useRef, useState } from 'react';
import {
  Activity,
  AudioLines,
  Check,
  Copy,
  Flame,
  Headphones,
  Power,
  RefreshCw,
  RotateCcw,
  ShieldCheck,
  Sliders,
  Sparkles,
  Volume2,
  Waves
} from 'lucide-react';
import {
  AudioDevice,
  BUILTIN_PRESETS,
  DEFAULT_PARAMS,
  DspParams,
  EQ_LABELS,
  LiveSourceStatus
} from './types';

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
  const [activeTab, setActiveTab] = useState<'dsp' | 'devices'>('dsp');
  const [syncStatus, setSyncStatus] = useState<'connected' | 'syncing' | 'offline'>('syncing');
  const [copiedCmd, setCopiedCmd] = useState<string | null>(null);
  const debounceRef = useRef<number | null>(null);

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
      const [devRes, paramRes, statusRes] = await Promise.all([
        fetch('/api/devices'),
        fetch('/api/params'),
        fetch('/api/status')
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
      setSyncStatus('connected');
    } catch {
      setSyncStatus('offline');
    }
  };

  useEffect(() => {
    void loadAll();
    const timer = window.setInterval(() => void loadStatus(), 1500);
    return () => window.clearInterval(timer);
  }, []);

  const pushParams = (next: DspParams, immediate = false) => {
    setParams(next);
    if (debounceRef.current) {
      window.clearTimeout(debounceRef.current);
    }

    const send = async () => {
      try {
        setSyncStatus('syncing');
        const res = await fetch('/api/params', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(next)
        });
        if (res.ok) {
          setSyncStatus('connected');
        } else {
          setSyncStatus('offline');
        }
      } catch {
        setSyncStatus('offline');
      }
    };

    if (immediate) {
      void send();
    } else {
      debounceRef.current = window.setTimeout(() => void send(), 45);
    }
  };

  const updateField = <K extends keyof DspParams>(
    key: K,
    value: DspParams[K],
    immediate = false
  ) => {
    pushParams({ ...params, [key]: value }, immediate);
  };

  const updateEqBand = (index: number, db: number) => {
    const nextBands = [...params.eqBandsDb];
    nextBands[index] = db;
    pushParams({ ...params, eqBandsDb: nextBands });
  };

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
                  ViPERHiRes
                </h1>
                <span className="rounded-md border border-emerald-500/30 bg-emerald-500/10 px-2 py-0.5 font-mono text-[11px] font-medium text-emerald-300">
                  44.1–384 kHz · 32-BIT FLOAT
                </span>
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

            {/* Sync Indicator */}
            <div
              className="flex items-center gap-1.5 rounded-lg border border-zinc-800 bg-zinc-950 px-2.5 py-1.5 text-xs"
              title="Shared Memory IPC status"
            >
              <span
                className={`h-2 w-2 rounded-full ${
                  syncStatus === 'connected'
                    ? 'bg-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.8)]'
                    : syncStatus === 'syncing'
                      ? 'bg-amber-400'
                      : 'bg-red-500'
                }`}
              />
              <span className="font-mono text-[11px] text-zinc-300">
                {syncStatus === 'connected'
                  ? 'LIVE'
                  : syncStatus === 'syncing'
                    ? 'SYNC'
                    : 'OFFLINE'}
              </span>
            </div>

            {/* Master Power */}
            <button
              type="button"
              onClick={() => updateField('masterEnabled', !params.masterEnabled, true)}
              className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3.5 py-2 text-xs font-semibold transition-all ${
                params.masterEnabled
                  ? 'border-emerald-400/50 bg-emerald-500/20 text-emerald-200 hover:bg-emerald-500/30'
                  : 'border-zinc-800 bg-zinc-950 text-zinc-400 hover:border-zinc-700 hover:text-zinc-200'
              }`}
            >
              <Power className="h-3.5 w-3.5" />
              {params.masterEnabled ? 'ENGINE ON' : 'BYPASS'}
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
                  <span className="rounded bg-zinc-900 px-2 py-0.5 font-mono text-[10px] text-zinc-400">
                    {liveStatus.mode}
                  </span>
                  {liveStatus.zeroResample && (
                    <span className="rounded border border-emerald-500/35 bg-emerald-500/10 px-2 py-0.5 font-mono text-[10px] font-semibold text-emerald-300">
                      0% RESAMPLE
                    </span>
                  )}
                  {liveStatus.isHiRes && (
                    <span className="rounded border border-amber-400/40 bg-amber-500/15 px-2 py-0.5 font-mono text-[10px] font-semibold text-amber-300">
                      HI-RES AUDIO
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
            {/* Presets Strip */}
            <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {BUILTIN_PRESETS.map(preset => (
                <button
                  key={preset.name}
                  type="button"
                  onClick={() => pushParams({ ...params, ...preset.params }, true)}
                  className="group cursor-pointer rounded-xl border border-zinc-800/90 bg-[#0c0c0c] p-3.5 text-left transition-colors hover:border-zinc-600 hover:bg-zinc-900/70"
                >
                  <div className="flex items-center justify-between">
                    <span className="font-display text-sm font-semibold text-zinc-100 group-hover:text-emerald-300">
                      {preset.name}
                    </span>
                    <Sparkles className="h-3.5 w-3.5 text-zinc-600 group-hover:text-emerald-400" />
                  </div>
                  <p className="mt-1 text-xs text-zinc-400">{preset.subtitle}</p>
                </button>
              ))}
            </section>

            {/* 10-Band Hi-Res Parametric Equalizer */}
            <section className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2.5">
                  <Activity className="h-4 w-4 text-emerald-400" />
                  <h2 className="font-display text-base font-semibold text-white">
                    10-Band Hi-Res Equalizer
                  </h2>
                  <span className="rounded bg-zinc-900 px-2 py-0.5 font-mono text-[11px] text-zinc-400">
                    64-bit Biquad
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={() =>
                      pushParams({ ...params, eqBandsDb: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0] }, true)
                    }
                    className="flex cursor-pointer items-center gap-1.5 rounded-lg border border-zinc-800 bg-zinc-950 px-2.5 py-1 text-xs text-zinc-300 transition-colors hover:border-zinc-700 hover:text-white"
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

              <EqCurveSvg bands={params.eqBandsDb} enabled={params.eqEnabled && params.masterEnabled} />

              <div className="mt-5 grid grid-cols-5 gap-2 sm:grid-cols-10">
                {EQ_LABELS.map((freq, idx) => {
                  const val = params.eqBandsDb[idx] ?? 0;
                  return (
                    <div key={freq} className="flex flex-col items-center gap-2">
                      <span className="font-mono text-[11px] tabular-nums text-zinc-300">
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
                      <span className="font-mono text-[11px] text-zinc-500">{freq}</span>
                    </div>
                  );
                })}
              </div>
            </section>

            {/* DSP Modules Grid */}
            <section className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
              {/* ViPER Bass */}
              <div className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5">
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Waves className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">ViPER Bass</h3>
                  </div>
                  <Toggle
                    checked={params.bassEnabled}
                    onChange={v => updateField('bassEnabled', v, true)}
                    label="Toggle ViPER Bass"
                  />
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
                      <span className="text-zinc-400">Bass Frequency</span>
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
                      <span className="text-zinc-400">Bass Boost</span>
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
              <div className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5">
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Sparkles className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">ViPER Clarity</h3>
                  </div>
                  <Toggle
                    checked={params.clarityEnabled}
                    onChange={v => updateField('clarityEnabled', v, true)}
                    label="Toggle ViPER Clarity"
                  />
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
                      <span className="text-zinc-400">Clarity Level</span>
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
              <div className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5">
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Flame className="h-4 w-4 text-amber-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Tube Simulator (6N1J)
                    </h3>
                  </div>
                  <Toggle
                    checked={params.tubeEnabled}
                    onChange={v => updateField('tubeEnabled', v, true)}
                    label="Toggle Tube Simulator"
                  />
                </div>

                <p className="mb-4 text-xs text-zinc-400">
                  Asymmetric even-order harmonic warmth with automatic sample-rate-invariant DC blocker.
                </p>

                <div>
                  <div className="mb-1.5 flex justify-between text-xs">
                    <span className="text-zinc-400">Warmth / Drive</span>
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
              <div className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5">
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Headphones className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Differential Surround
                    </h3>
                  </div>
                  <Toggle
                    checked={params.surroundEnabled}
                    onChange={v => updateField('surroundEnabled', v, true)}
                    label="Toggle Differential Surround"
                  />
                </div>

                <div className="space-y-4">
                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="text-zinc-400">Haas Delay</span>
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
                      <span className="text-zinc-400">Stereo Soundstage</span>
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
              <div className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5">
                <div className="mb-4 flex items-center gap-2">
                  <Volume2 className="h-4 w-4 text-zinc-200" />
                  <h3 className="font-display text-sm font-semibold text-white">Master Output</h3>
                </div>

                <div className="space-y-4">
                  <div>
                    <div className="mb-1.5 flex justify-between text-xs">
                      <span className="text-zinc-400">Output Gain</span>
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
                      <span className="text-zinc-400">L / R Balance</span>
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
              <div className="rounded-2xl border border-zinc-800/90 bg-[#0c0c0c] p-5">
                <div className="mb-4 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <ShieldCheck className="h-4 w-4 text-emerald-400" />
                    <h3 className="font-display text-sm font-semibold text-white">
                      Peak Limiter
                    </h3>
                  </div>
                  <Toggle
                    checked={params.limiterEnabled}
                    onChange={v => updateField('limiterEnabled', v, true)}
                    label="Toggle Peak Limiter"
                  />
                </div>

                <p className="mb-4 text-xs text-zinc-400">
                  Prevents digital clipping above 0 dBFS when stacking heavy Bass & EQ boosts.
                </p>

                <div>
                  <div className="mb-1.5 flex justify-between text-xs">
                    <span className="text-zinc-400">Ceiling Threshold</span>
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
