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

/** Sérialisé en nombre par System.Text.Json, dans l'ordre de déclaration de l'enum C#. */
export const MusicLink = { NotLinked: 0, NoActiveDevice: 1, Ready: 2, Denied: 3 } as const;

export type MusicState = {
  link: number;
  nowPlaying: NowPlaying;
};

export type MusicDevice = {
  id: string;
  name: string;
  isActive: boolean;
  type: string;
};

export type Playlist = {
  name: string;
  uri: string;
};

/** Capacités déduites de l'inventaire Zigbee : l'UI s'adapte à chaque ampoule. */
export type LightCapabilities = {
  brightness: boolean;
  color: boolean;
  colorTemp: boolean;
  colorTempMin: number | null;
  colorTempMax: number | null;
  effects: string[];
  powerOnBehaviours: string[];
};

export type LightSnapshot = {
  id: string;
  name: string;
  on: boolean;
  brightness: number | null;
  colorHex: string | null;
  /** Faux quand Zigbee2MQTT signale la lampe injoignable, coupée au mur par exemple. */
  reachable: boolean;
  /** Faux tant que la lampe n'est pas dans l'inventaire Zigbee : elle n'est pas appairée. */
  paired: boolean;
  /** Température de blanc en mireds. Plus la valeur est basse, plus c'est froid. */
  colorTempMired: number | null;
  linkQuality: number | null;
  powerOnBehavior: string | null;
  capabilities: LightCapabilities;
};

export type SceneInfo = {
  id: string;
  name: string;
  icon: string;
  /** Vrai si la scène éteint un PC : l'UI demande alors une confirmation. */
  destructive: boolean;
};

export type SceneStarted = { runId: string; sceneId: string };
export type SceneStepCompleted = {
  runId: string;
  stepIndex: number;
  status: string;
  message: string | null;
};
export type SceneFinished = { runId: string; status: string };

export type StateSnapshot = {
  room: { id: string; name: string };
  pcs: PcSnapshot[];
  audio: Record<string, AudioSnapshot>;
  lights: LightSnapshot[];
  music: MusicState;
  scenes: SceneInfo[];
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

export type LightStateChanged = {
  id: string;
  on: boolean;
  brightness: number | null;
  colorHex: string | null;
  reachable: boolean;
  colorTempMired: number | null;
  linkQuality: number | null;
  powerOnBehavior: string | null;
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
