import { useState } from 'react';
import { UnauthorizedError, runPcAction } from '../api/client';
import type { PcAction } from '../api/client';

type Status = 'idle' | 'pending' | 'failed';

/**
 * Trois états : idle, pending, résultat. Aucune mise à jour optimiste : l'état
 * affiché vient toujours du serveur, via le hub. Un bouton qui ment est pire
 * qu'un bouton lent (docs/07-frontend.md).
 */
export function ActionButton({
  pcId,
  action,
  label,
  disabled,
  onUnauthorized,
}: {
  pcId: string;
  action: PcAction;
  label: string;
  disabled: boolean;
  onUnauthorized: () => void;
}) {
  const [status, setStatus] = useState<Status>('idle');
  const [error, setError] = useState<string | null>(null);

  async function run() {
    setStatus('pending');
    setError(null);

    try {
      await runPcAction(pcId, action);
      setStatus('idle');
    } catch (cause) {
      if (cause instanceof UnauthorizedError) {
        onUnauthorized();
        return;
      }
      setStatus('failed');
      setError(cause instanceof Error ? cause.message : 'Échec');
    }
  }

  return (
    <div className="flex flex-col gap-1">
      <button
        type="button"
        disabled={disabled || status === 'pending'}
        onClick={run}
        className="min-h-11 min-w-24 rounded-lg border border-neutral-800 bg-neutral-900 px-4 text-neutral-200 transition-colors active:bg-neutral-800 disabled:opacity-40"
      >
        {status === 'pending' ? '…' : label}
      </button>
      {error && <span className="text-xs text-red-400">{error}</span>}
    </div>
  );
}
