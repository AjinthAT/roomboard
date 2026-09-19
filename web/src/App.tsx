import { useCallback, useEffect, useState } from 'react';
import { UnauthorizedError, getState } from './api/client';
import { connectRoomHub } from './api/hub';
import { clearToken, readToken } from './api/token';
import { AudioCard } from './components/AudioCard';
import { LightsCard } from './components/LightsCard';
import { MusicCard } from './components/MusicCard';
import { PcCard } from './components/PcCard';
import { RoutinesCard } from './components/RoutinesCard';
import { SceneBar } from './components/SceneBar';
import { TokenGate } from './components/TokenGate';
import { roomStore, useRoom } from './store/roomStore';

type Phase = 'need-token' | 'loading' | 'ready' | 'error';

export function App() {
  const [phase, setPhase] = useState<Phase>(() => (readToken() ? 'loading' : 'need-token'));
  const [error, setError] = useState<string | null>(null);

  const forgetToken = useCallback(() => {
    clearToken();
    roomStore.reset();
    setPhase('need-token');
  }, []);

  // Chargement : snapshot complet avant toute chose.
  useEffect(() => {
    if (phase !== 'loading') {
      return;
    }

    const controller = new AbortController();

    getState(controller.signal)
      .then((snapshot) => {
        roomStore.loadSnapshot(snapshot);
        setPhase('ready');
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) {
          return;
        }
        if (cause instanceof UnauthorizedError) {
          forgetToken();
          return;
        }
        setError(cause instanceof Error ? cause.message : 'Erreur inconnue');
        setPhase('error');
      });

    return () => controller.abort();
  }, [phase, forgetToken]);

  // Hub : vivant tant qu'on est en phase « ready », et pas une milliseconde de moins.
  //
  // Ouvrir la connexion dans l'effet de chargement était un piège : cet effet dépend
  // de `phase`, qu'il modifie lui-même en passant à « ready ». Le nettoyage refermait
  // donc aussitôt la connexion qu'il venait d'ouvrir, et la nouvelle exécution sortait
  // sans la rouvrir. Le panneau restait sur le dernier snapshot, figé, sans jamais
  // recevoir un seul delta.
  useEffect(() => {
    if (phase !== 'ready') {
      return;
    }

    const hub = connectRoomHub();
    return () => hub.stop();
  }, [phase]);

  if (phase === 'need-token') {
    return (
      <Shell>
        <TokenGate onSubmit={() => setPhase('loading')} />
      </Shell>
    );
  }

  if (phase === 'loading') {
    return (
      <Shell>
        <p className="text-neutral-500">Connexion au Core…</p>
      </Shell>
    );
  }

  if (phase === 'error') {
    return (
      <Shell>
        <p className="text-neutral-300">Core injoignable</p>
        <p className="text-sm text-neutral-600">{error}</p>
        <button
          type="button"
          onClick={() => setPhase('loading')}
          className="min-h-11 rounded-lg border border-neutral-800 px-4 text-neutral-300"
        >
          Réessayer
        </button>
      </Shell>
    );
  }

  return (
    <main className="mx-auto flex h-full max-w-5xl flex-col gap-6 p-6">
      <Header />
      <ConnectionBanner />
      <div className="grid gap-6 md:grid-cols-2">
        <PcCard onUnauthorized={forgetToken} />
        <MusicCard onUnauthorized={forgetToken} />
        <AudioCard onUnauthorized={forgetToken} />
        <LightsCard onUnauthorized={forgetToken} />
        <RoutinesCard onUnauthorized={forgetToken} />
      </div>

      <SceneBar onUnauthorized={forgetToken} />
    </main>
  );
}

function Header() {
  const roomName = useRoom((s) => s.roomName);

  return (
    <header className="flex items-baseline justify-between">
      <h1 className="text-sm font-medium uppercase tracking-[0.3em] text-neutral-500">Room Control</h1>
      <span className="text-sm text-neutral-600">{roomName}</span>
    </header>
  );
}

/**
 * Bandeau discret. Les contrôles restent visibles mais l'état affiché peut être
 * périmé : on le dit, on ne masque pas l'interface (docs/07-frontend.md).
 */
function ConnectionBanner() {
  const connection = useRoom((s) => s.connection);

  if (connection === 'connected') {
    return null;
  }

  const message =
    connection === 'connecting' ? 'Connexion…'
    : connection === 'reconnecting' ? 'Reconnexion…'
    : 'Connexion perdue';

  return (
    <p className="rounded-lg border border-amber-900/50 bg-amber-950/30 px-4 py-2 text-sm text-amber-200/80">
      {message}
    </p>
  );
}

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <main className="flex h-full flex-col items-center justify-center gap-4 p-8">
      <h1 className="text-sm font-medium uppercase tracking-[0.3em] text-neutral-500">Room Control</h1>
      {children}
    </main>
  );
}
