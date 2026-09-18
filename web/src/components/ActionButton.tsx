import { useState } from 'react';
import { UnauthorizedError, runPcAction } from '../api/client';
import type { PcAction } from '../api/client';
import { useConfirm } from './useConfirm';

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
  confirm = false,
  onUnauthorized,
}: {
  pcId: string;
  action: PcAction;
  label: string;
  disabled: boolean;
  /** Demande un second appui. Réservé aux actions qu'on ne peut pas défaire. */
  confirm?: boolean;
  onUnauthorized: () => void;
}) {
  const [status, setStatus] = useState<Status>('idle');
  const [error, setError] = useState<string | null>(null);
  const { armed, arm, disarm } = useConfirm();

  async function run() {
    disarm();
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
        onClick={confirm && !armed ? arm : run}
        className={`min-h-11 min-w-24 rounded-lg border px-4 transition-colors disabled:opacity-40 ${
          armed
            ? 'border-amber-600 bg-amber-950/40 text-amber-200'
            : 'border-neutral-800 bg-neutral-900 text-neutral-200 active:bg-neutral-800'
        }`}
      >
        {status === 'pending' ? '…' : armed ? 'Confirmer ?' : label}
      </button>
      {error && <span className="text-xs text-red-400">{error}</span>}
    </div>
  );
}
