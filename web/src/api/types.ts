/**
 * Types du contrat d'API. Tenus synchrones à la main avec RoomOS.Domain.
 * Voir docs/11-conventions.md.
 */

export type Telemetry = {
  cpuUsage: number;
  cpuTempC: number | null;
  gpuUsage: number;
  gpuTempC: number | null;
  vramUsedMb: number | null;
  vramTotalMb: number | null;
  ramUsedMb: number;
  ramTotalMb: number;
};

export type PcSnapshot = {
  id: string;
  name: string;
  online: boolean;
  uptimeSec: number | null;
  telemetry: Telemetry | null;
};

export type AudioOutputInfo = {
  id: string;
  name: string;
  connected: boolean;
};

export type AudioSnapshot = {
  /** null est un cas nominal : sortie active hors du registre. */
  activeOutputId: string | null;
  volume: number;
  muted: boolean;
  outputs: AudioOutputInfo[];
};

export type NowPlaying = {
  title: string | null;
  artist: string | null;
  albumArtUrl: string | null;
  isPlaying: boolean;
  progressMs: number | null;
  durationMs: number | null;
  deviceName: string | null;
};

/** Sérialisé en nombre par System.Text.Json : 0 NotLinked, 1 NoActiveDevice, 2 Ready. */
export const MusicLink = { NotLinked: 0, NoActiveDevice: 1, Ready: 2 } as const;

export type MusicState = {
  link: number;
  nowPlaying: NowPlaying;
};

export type StateSnapshot = {
  room: { id: string; name: string };
  pcs: PcSnapshot[];
  audio: Record<string, AudioSnapshot>;
  music: MusicState;
  serverTime: string;
};

export type PcStateChanged = {
  id: string;
  online: boolean;
  uptimeSec: number | null;
};

export type TelemetryUpdated = {
  id: string;
  telemetry: Telemetry;
};

export type NowPlayingChanged = {
  link: number;
  nowPlaying: NowPlaying;
};

export type AudioStateChanged = {
  pcId: string;
  activeOutputId: string | null;
  volume: number;
  muted: boolean;
  outputs: AudioOutputInfo[];
};
