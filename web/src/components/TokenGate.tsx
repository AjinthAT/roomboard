import { useState } from 'react';
import { writeToken } from '../api/token';

/**
 * Saisie unique du jeton. Volontairement rudimentaire : c'est un panneau fixe
 * sur un LAN, pas un portail d'authentification.
 */
export function TokenGate({ onSubmit }: { onSubmit: () => void }) {
  const [value, setValue] = useState('');

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        if (!value.trim()) {
          return;
        }
        writeToken(value.trim());
        onSubmit();
      }}
    >
      <label className="text-sm text-neutral-400" htmlFor="token">
        Jeton d'accès
      </label>
      <input
        id="token"
        type="password"
        value={value}
        autoComplete="off"
        onChange={(event) => setValue(event.target.value)}
        className="min-h-11 rounded-lg border border-neutral-800 bg-neutral-900 px-4 text-neutral-100 outline-none focus:border-neutral-600"
      />
      <button
        type="submit"
        className="min-h-11 rounded-lg bg-neutral-100 px-4 font-medium text-neutral-900 active:bg-neutral-300"
      >
        Connecter
      </button>
    </form>
  );
}
