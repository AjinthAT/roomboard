import { readToken } from './token';
import type {
  MusicDevice, MusicTrack, Playlist, SceneDetail, SceneStep, StateSnapshot,
} from './types';

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

export type RoutinePatch = {
  name?: string;
  sceneId?: string;
  time?: string;
  days?: boolean[];
  enabled?: boolean;
};

export function saveRoutine(id: string, patch: RoutinePatch): Promise<unknown> {
  return request(`/api/routines/${encodeURIComponent(id)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(patch),
  });
}

export function getScene(id: string, signal?: AbortSignal): Promise<SceneDetail> {
  return request<SceneDetail>(`/api/scenes/${encodeURIComponent(id)}`, { signal });
}

export function createScene(body: {
  name: string; icon?: string; steps: SceneStep[];
}): Promise<{ id: string }> {
  return request<{ id: string }>('/api/scenes', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export function updateScene(
  id: string,
  body: { name?: string; icon?: string; steps?: SceneStep[] },
): Promise<unknown> {
  return request(`/api/scenes/${encodeURIComponent(id)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export function deleteScene(id: string): Promise<unknown> {
  return request(`/api/scenes/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

export function createRoutine(body: {
  name: string; sceneId: string; time: string; days?: boolean[];
}): Promise<{ id: string }> {
  return request<{ id: string }>('/api/routines', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export function deleteRoutine(id: string): Promise<unknown> {
  return request(`/api/routines/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

export function runScene(id: string): Promise<{ runId: string }> {
  return request<{ runId: string }>(`/api/scenes/${encodeURIComponent(id)}/run`, {
    method: 'POST',
  });
}

export const setShuffle = (enabled: boolean) =>
  putJson('/api/music/shuffle', { enabled });

export const setRepeat = (mode: 'off' | 'track' | 'context') =>
  putJson('/api/music/repeat', { mode });

export const seekMusic = (positionMs: number) =>
  putJson('/api/music/seek', { positionMs });

export function getQueue(signal?: AbortSignal): Promise<MusicTrack[]> {
  return request<MusicTrack[]>('/api/music/queue', { signal });
}

export function queueTrack(uri: string): Promise<unknown> {
  return request('/api/music/queue', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ uri }),
  });
}

export function searchTracks(query: string, signal?: AbortSignal): Promise<MusicTrack[]> {
  return request<MusicTrack[]>(`/api/music/search?q=${encodeURIComponent(query)}`, { signal });
}

function putJson(path: string, body: unknown): Promise<unknown> {
  return request(path, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
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
