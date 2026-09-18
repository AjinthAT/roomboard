const STORAGE_KEY = 'roomos.token';

/**
 * Le jeton est saisi une fois puis gardé en localStorage (docs/05-api.md).
 * L'accès est protégé : Safari le refuse en navigation privée.
 */
export function readToken(): string | null {
  try {
    return window.localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

export function writeToken(token: string): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, token);
  } catch {
    // Sans stockage, le jeton vit le temps de la session. L'app reste utilisable.
  }
}

export function clearToken(): void {
  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Rien à faire.
  }
}
