import { useEffect, useState } from 'react';
import { getHealth } from './api/client';
import type { HealthResponse } from './api/types';

type CoreStatus =
  | { kind: 'loading' }
  | { kind: 'up'; health: HealthResponse }
  | { kind: 'down'; reason: string };

export function App() {
  const [status, setStatus] = useState<CoreStatus>({ kind: 'loading' });

  useEffect(() => {
    const controller = new AbortController();

    getHealth(controller.signal)
      .then((health) => setStatus({ kind: 'up', health }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return;
        }
        setStatus({ kind: 'down', reason: error instanceof Error ? error.message : 'erreur inconnue' });
      });

    return () => controller.abort();
  }, []);

  return (
    <main className="flex h-full flex-col items-center justify-center gap-6 p-8">
      <h1 className="text-sm font-medium uppercase tracking-[0.3em] text-neutral-500">Room Control</h1>
      <StatusBadge status={status} />
    </main>
  );
}

function StatusBadge({ status }: { status: CoreStatus }) {
  if (status.kind === 'loading') {
    return <p className="text-neutral-500">Connexion au Core…</p>;
  }

  if (status.kind === 'down') {
    return (
      <div className="flex flex-col items-center gap-2">
        <Dot className="bg-red-500" />
        <p className="text-neutral-300">Core injoignable</p>
        <p className="text-sm text-neutral-600">{status.reason}</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center gap-2">
      <Dot className="bg-emerald-500" />
      <p className="text-neutral-300">Core en ligne</p>
      <p className="text-sm text-neutral-600">version {status.health.version}</p>
    </div>
  );
}

function Dot({ className }: { className: string }) {
  return <span className={`size-3 rounded-full ${className}`} />;
}
