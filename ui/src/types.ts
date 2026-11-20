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
}

export const EQ_LABELS = ['31', '62', '125', '250', '500', '1k', '2k', '4k', '8k', '16k'];

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
  limiterThresholdDb: -0.3
};

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
