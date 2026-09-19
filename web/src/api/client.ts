import { readToken } from './token';
import type { MusicDevice, Playlist, StateSnapshot } from './types';

/**
 * Le front n'appelle que le Core, sur la même origine : pas de base URL,
 * pas de CORS (docs/03-architecture.md).
 */
export class UnauthorizedError extends Error {
  constructor() {
    super('Jeton refusé par le Core.');
    this.name = 'UnauthorizedError';
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = readToken();

  const response = await fetch(path, {
    ...init,
    headers: {
      ...init?.headers,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });

  if (response.status === 401 || response.status === 403) {
    throw new UnauthorizedError();
  }

  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as { message?: string } | null;
    throw new Error(body?.message ?? `${path} a répondu ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function getState(signal?: AbortSignal): Promise<StateSnapshot> {
  return request<StateSnapshot>('/api/state', { signal });
}

export type PcAction = 'wake' | 'shutdown' | 'restart';

export function runPcAction(pcId: string, action: PcAction): Promise<unknown> {
  return request(`/api/pc/${encodeURIComponent(pcId)}/${action}`, { method: 'POST' });
}

function putAudio(pcId: string, path: string, body: unknown): Promise<unknown> {
  return request(`/api/audio/${encodeURIComponent(pcId)}/${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export const setAudioOutput = (pcId: string, outputId: string) =>
  putAudio(pcId, 'output', { outputId });

export const setAudioVolume = (pcId: string, level: number) =>
  putAudio(pcId, 'volume', { level });

export const setAudioMute = (pcId: string, muted: boolean) =>
  putAudio(pcId, 'mute', { muted });

export type LightCommand = {
  on?: boolean;
  brightness?: number;
  colorHex?: string;
  colorTempMired?: number;
  effect?: string;
  powerOnBehavior?: string;
  /** Durée du fondu en secondes. Appliquée à toute la commande par Zigbee2MQTT. */
  transitionSec?: number;
};

export function identifyLight(id: string): Promise<unknown> {
  return request(`/api/lights/${encodeURIComponent(id)}/identify`, { method: 'POST' });
}

export function setLight(id: string, command: LightCommand): Promise<unknown> {
  return request(`/api/lights/${encodeURIComponent(id)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(command),
  });
}

export function runScene(id: string): Promise<{ runId: string }> {
  return request<{ runId: string }>(`/api/scenes/${encodeURIComponent(id)}/run`, {
    method: 'POST',
  });
}

export type MusicAction = 'play' | 'pause' | 'next' | 'previous';

export function runMusicAction(action: MusicAction): Promise<unknown> {
  return request(`/api/music/${action}`, { method: 'POST' });
}

export function getMusicDevices(signal?: AbortSignal): Promise<MusicDevice[]> {
  return request<MusicDevice[]>('/api/music/devices', { signal });
}

export function getPlaylists(signal?: AbortSignal): Promise<Playlist[]> {
  return request<Playlist[]>('/api/music/playlists', { signal });
}

export function transferMusic(deviceId: string): Promise<unknown> {
  return request('/api/music/transfer', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ deviceId }),
  });
}

export function playUri(uri: string): Promise<unknown> {
  return request('/api/music/play', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ uri }),
  });
}

export function setMusicVolume(level: number): Promise<unknown> {
  return request('/api/music/volume', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ level }),
  });
}
