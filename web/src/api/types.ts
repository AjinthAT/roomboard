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

export type StateSnapshot = {
  room: { id: string; name: string };
  pcs: PcSnapshot[];
  audio: Record<string, AudioSnapshot>;
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

export type AudioStateChanged = {
  pcId: string;
  activeOutputId: string | null;
  volume: number;
  muted: boolean;
  outputs: AudioOutputInfo[];
};
