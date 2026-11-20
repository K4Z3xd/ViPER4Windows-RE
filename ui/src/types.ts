export interface AudioDevice {
  id: string;
  guid: string;
  name: string;
  sampleRate: number;
  channels: number;
  bitsPerSample: number;
  isDefault: boolean;
}

export interface LiveSourceStatus {
  active: boolean;
  source: string;
  mode: string;
  sampleRate: number;
  channels: number;
  bitDepth: number;
  bitrateKbps: number;
  codec: string;
  title: string;
  artist: string;
  deviceName: string;
  isHiRes: boolean;
  zeroResample: boolean;
  extraApps?: string[];
}

export interface DspParams {
  sequence?: number;
  masterEnabled: boolean;
  masterGainDb: number;
  outputPan: number;
  eqEnabled: boolean;
  eqBandsDb: number[];
  bassEnabled: boolean;
  bassMode: number; // 0: Natural, 1: PureBass+, 2: Subwoofer
  bassFrequencyHz: number;
  bassGainDb: number;
  clarityEnabled: boolean;
  clarityMode: number; // 0: Natural, 1: OZone+, 2: XHiFi
  clarityGainDb: number;
  tubeEnabled: boolean;
  tubeDrive: number;
  surroundEnabled: boolean;
  surroundDelayMs: number;
  stereoWidth: number;
  limiterEnabled: boolean;
  limiterThresholdDb: number;
  // V2 Classic ViPER4Windows Modules
  dynamicEnabled: boolean;
  dynamicDevice: number; // 0..3
  dynamicBass: number;   // 0..100
  reverbEnabled: boolean;
  reverbRoomSize: number;
  reverbWidth: number;
  reverbDamping: number;
  reverbWet: number;
  reverbDry: number;
  compEnabled: boolean;
  compThresholdDb: number;
  compRatio: number;
  compAttackMs: number;
  compReleaseMs: number;
  compMakeupDb: number;
  cureEnabled: boolean;
  cureLevel: number; // 0: Slight, 1: Moderate, 2: Extreme
  // V3 Convolver
  convolverEnabled: boolean;
  convolverCrossChannel: number;
  convolverWetMix: number;
  convolverRevision: number;
  // V4 Multi-Band EQ
  eqMode: number; // 0 = 10-Band, 1 = 18-Band
  eqBands18Db: number[];
  // V5 Full V4A Parity Modules
  ddcEnabled: boolean;
  ddcRevision: number;
  vseEnabled: boolean;
  vseStrength: number;
  analogXEnabled: boolean;
  analogXLevel: number; // 0: Slight, 1: Moderate, 2: Extreme
  fieldSurroundEnabled: boolean;
  fieldSurroundStrength: number;
  fieldSurroundMidImage: number;
  vhsEnabled: boolean;
  vhsLevel: number; // 1..5
  agcEnabled: boolean;
  agcStrength: number;
  agcMaxGainDb: number;
  agcOutputThreshDb: number;
}

export const EQ_LABELS_10 = ['31', '62', '125', '250', '500', '1k', '2k', '4k', '8k', '16k'];
export const EQ_LABELS_18 = [
  '31', '45', '62', '90', '125', '180',
  '250', '360', '500', '750', '1k', '1.5k',
  '2k', '3k', '4k', '6k', '8k', '16k'
];
export const EQ_LABELS = EQ_LABELS_10;

// Maps 10-band curve -> 18-band curve with smooth midpoint interpolation (rounded to 0.5 dB)
export function interpolate10to18(b10: number[]): number[] {
  const g = (i: number) => Number(b10[i] ?? 0);
  const mid = (a: number, b: number) => Math.round(((a + b) / 2) * 2) / 2;
  return [
    g(0),                 // 31
    mid(g(0), g(1)),      // 45
    g(1),                 // 62
    mid(g(1), g(2)),      // 90
    g(2),                 // 125
    mid(g(2), g(3)),      // 180
    g(3),                 // 250
    mid(g(3), g(4)),      // 360
    g(4),                 // 500
    mid(g(4), g(5)),      // 750
    g(5),                 // 1k
    mid(g(5), g(6)),      // 1.5k
    g(6),                 // 2k
    mid(g(6), g(7)),      // 3k
    g(7),                 // 4k
    mid(g(7), g(8)),      // 6k
    g(8),                 // 8k
    g(9)                  // 16k
  ];
}

// Extracts 10 anchor bands from 18-band curve
export function downsample18to10(b18: number[]): number[] {
  const idx = [0, 2, 4, 6, 8, 10, 12, 14, 16, 17];
  return idx.map(i => Number(b18[i] ?? 0));
}

export const DEFAULT_PARAMS: DspParams = {
  masterEnabled: true,
  masterGainDb: 0,
  outputPan: 0,
  eqEnabled: false,
  eqBandsDb: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
  bassEnabled: false,
  bassMode: 0,
  bassFrequencyHz: 60,
  bassGainDb: 3.5,
  clarityEnabled: false,
  clarityMode: 0,
  clarityGainDb: 3.5,
  tubeEnabled: false,
  tubeDrive: 0.25,
  surroundEnabled: false,
  surroundDelayMs: 6.0,
  stereoWidth: 1.0,
  limiterEnabled: true,
  limiterThresholdDb: -0.3,
  dynamicEnabled: false,
  dynamicDevice: 0,
  dynamicBass: 25,
  reverbEnabled: false,
  reverbRoomSize: 0.35,
  reverbWidth: 0.5,
  reverbDamping: 0.25,
  reverbWet: 0.2,
  reverbDry: 0.85,
  compEnabled: false,
  compThresholdDb: -18,
  compRatio: 3,
  compAttackMs: 10,
  compReleaseMs: 120,
  compMakeupDb: 3,
  cureEnabled: false,
  cureLevel: 0,
  convolverEnabled: false,
  convolverCrossChannel: 0,
  convolverWetMix: 1,
  convolverRevision: 0,
  eqMode: 0,
  eqBands18Db: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
  ddcEnabled: false,
  ddcRevision: 0,
  vseEnabled: false,
  vseStrength: 0.5,
  analogXEnabled: false,
  analogXLevel: 0,
  fieldSurroundEnabled: false,
  fieldSurroundStrength: 0.4,
  fieldSurroundMidImage: 0.5,
  vhsEnabled: false,
  vhsLevel: 2,
  agcEnabled: false,
  agcStrength: 0.5,
  agcMaxGainDb: 12,
  agcOutputThreshDb: -1
};

export interface EqPreset {
  name: string;
  eqMode: number; // 0 = 10-Band, 1 = 18-Band
  eqBandsDb?: number[];
  eqBands18Db?: number[];
}

export const BUILTIN_EQ_PRESETS: EqPreset[] = [
  {
    name: 'Flat',
    eqMode: 0,
    eqBandsDb: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    eqBands18Db: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
  },
  {
    name: 'V-Shape Fun',
    eqMode: 0,
    eqBandsDb: [3.5, 4.0, 2.0, 0, -1.5, -1.0, 0.5, 2.0, 3.5, 3.0],
    eqBands18Db: [3.5, 4.0, 4.0, 3.0, 2.0, 1.0, 0, -1.0, -1.5, -1.5, -1.0, 0, 0.5, 1.5, 2.0, 3.0, 3.5, 3.0]
  },
  {
    name: 'Vocal Focus',
    eqMode: 0,
    eqBandsDb: [-1.0, -0.5, 0, 1.0, 2.0, 3.0, 3.5, 2.0, 0.5, 0],
    eqBands18Db: [-1.0, -1.0, -0.5, 0, 0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 3.5, 2.5, 2.0, 1.0, 0.5, 0]
  },
  {
    name: 'Deep Basshead',
    eqMode: 1,
    eqBandsDb: [5.5, 6.0, 3.5, 1.0, -1.0, -0.5, 0.5, 1.5, 2.0, 1.5],
    eqBands18Db: [5.5, 6.0, 6.0, 4.5, 3.5, 2.0, 1.0, 0, -1.0, -1.0, -0.5, 0, 0.5, 1.0, 1.5, 2.0, 2.0, 1.5]
  },
  {
    name: 'Acoustic & Jazz',
    eqMode: 1,
    eqBandsDb: [1.0, 1.5, 1.0, 0.5, 0, 0.5, 1.5, 2.0, 2.5, 2.0],
    eqBands18Db: [1.0, 1.5, 1.5, 1.0, 1.0, 0.5, 0.5, 0, 0, 0.5, 0.5, 1.0, 1.5, 1.5, 2.0, 2.5, 2.5, 2.0]
  }
];

export interface Preset {
  name: string;
  subtitle: string;
  params: Partial<DspParams>;
}

export const BUILTIN_PRESETS: Preset[] = [
  {
    name: 'Flat Reference',
    subtitle: 'Transparent bit-clean studio baseline',
    params: {
      eqEnabled: false,
      eqBandsDb: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
      bassEnabled: false,
      clarityEnabled: false,
      tubeEnabled: false,
      surroundEnabled: false,
      masterGainDb: 0
    }
  },
  {
    name: 'Hi-Res Audiophile',
    subtitle: 'Extended sub-bass + XHiFi micro-detail',
    params: {
      eqEnabled: true,
      eqBandsDb: [1.5, 2.0, 0.5, 0, -0.5, 0, 0.5, 1.5, 2.5, 2.0],
      bassEnabled: true,
      bassMode: 1,
      bassFrequencyHz: 55,
      bassGainDb: 3.5,
      clarityEnabled: true,
      clarityMode: 2,
      clarityGainDb: 3.0,
      tubeEnabled: false,
      surroundEnabled: true,
      surroundDelayMs: 3.5,
      stereoWidth: 1.15
    }
  },
  {
    name: 'Deep Bass Boost',
    subtitle: 'Subwoofer harmonic punch for IEMs',
    params: {
      eqEnabled: true,
      eqBandsDb: [4.5, 5.0, 2.5, 0.5, -1.0, -0.5, 0.5, 1.5, 2.0, 1.5],
      bassEnabled: true,
      bassMode: 2,
      bassFrequencyHz: 65,
      bassGainDb: 7.5,
      clarityEnabled: true,
      clarityMode: 0,
      clarityGainDb: 2.5,
      tubeEnabled: false,
      surroundEnabled: false
    }
  },
  {
    name: 'Warm Tube Studio',
    subtitle: '6N1J analog warmth + vocal focus',
    params: {
      eqEnabled: true,
      eqBandsDb: [1.0, 1.5, 1.0, 0.5, 0.5, 1.0, 1.5, 1.0, 0.5, 0],
      bassEnabled: true,
      bassMode: 0,
      bassFrequencyHz: 60,
      bassGainDb: 2.5,
      clarityEnabled: true,
      clarityMode: 1,
      clarityGainDb: 2.0,
      tubeEnabled: true,
      tubeDrive: 0.42,
      surroundEnabled: true,
      surroundDelayMs: 5.0,
      stereoWidth: 1.1
    }
  }
];
