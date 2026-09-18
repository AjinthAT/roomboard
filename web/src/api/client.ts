import type { HealthResponse } from './types';

/**
 * Le front n'appelle que le Core, sur la même origine : pas de base URL,
 * pas de CORS. Voir docs/03-architecture.md.
 */
async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(path, { signal });

  if (!response.ok) {
    throw new Error(`${path} a répondu ${response.status}`);
  }

  return (await response.json()) as T;
}

export function getHealth(signal?: AbortSignal): Promise<HealthResponse> {
  return get<HealthResponse>('/healthz', signal);
}
