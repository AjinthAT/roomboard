import { readToken } from './token';
import type { HealthResponse, StateSnapshot } from './types';

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

export function getHealth(signal?: AbortSignal): Promise<HealthResponse> {
  return request<HealthResponse>('/healthz', { signal });
}

export function getState(signal?: AbortSignal): Promise<StateSnapshot> {
  return request<StateSnapshot>('/api/state', { signal });
}

export type PcAction = 'wake' | 'shutdown' | 'restart';

export function runPcAction(pcId: string, action: PcAction): Promise<unknown> {
  return request(`/api/pc/${encodeURIComponent(pcId)}/${action}`, { method: 'POST' });
}
