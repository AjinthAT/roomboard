import { useEffect, useState } from 'react';
import { UnauthorizedError, setAudioMute, setAudioOutput, setAudioVolume } from '../api/client';
import { useRoom } from '../store/roomStore';

export function AudioCard({ onUnauthorized }: { onUnauthorized: () => void }) {
  const pcId = useRoom((s) => s.pc?.id ?? null);
  const live = useRoom((s) => s.connection === 'connected');
  const online = useRoom((s) => s.pc?.online ?? false);
  const activeOutputId = useRoom((s) => s.audio?.activeOutputId ?? null);
  const muted = useRoom((s) => s.audio?.muted ?? false);
  const outputCount = useRoom((s) => s.audio?.outputs.length ?? 0);

  const [error, setError] = useState<string | null>(null);

  if (!pcId) {
    return null;
  }

  const usable = live && online;

  async function run(action: () => Promise<unknown>) {
    setError(null);
    try {
      await action();
    } catch (cause) {
      if (cause instanceof UnauthorizedError) {
        onUnauthorized();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Échec');
    }
  }

  return (
    <section className="flex flex-col gap-5 rounded-2xl border border-neutral-800 bg-neutral-950 p-6">
      <header className="flex items-baseline justify-between">
        <h2 className="text-xs font-medium uppercase tracking-[0.2em] text-neutral-500">Audio</h2>
        {/* Sortie active inconnue : cas nominal, pas une erreur (docs/04-domaine.md). */}
        {usable && activeOutputId === null && outputCount > 0 && (
          <span className="text-sm text-neutral-500">Sortie inconnue</span>
        )}
      </header>

      <OutputPicker
        pcId={pcId}
        activeOutputId={activeOutputId}
        usable={usable}
        onPick={(outputId) => run(() => setAudioOutput(pcId, outputId))}
      />

      <VolumeRow
        muted={muted}
        usable={usable}
        onVolume={(level) => run(() => setAudioVolume(pcId, level))}
        onMute={() => run(() => setAudioMute(pcId, !muted))}
      />

      {error && <p className="text-xs text-red-400">{error}</p>}
    </section>
  );
}

function OutputPicker({
  activeOutputId,
  usable,
  onPick,
}: {
  pcId: string;
  activeOutputId: string | null;
  usable: boolean;
  onPick: (outputId: string) => void;
}) {
  const outputs = useRoom((s) => s.audio?.outputs ?? EMPTY_OUTPUTS);

  if (outputs.length === 0) {
    return <p className="text-sm text-neutral-600">Aucune sortie détectée.</p>;
  }

  return (
    <div className="flex flex-wrap gap-3">
      {outputs.map((output) => {
        const active = output.id === activeOutputId;

        return (
          <button
            key={output.id}
            type="button"
            // Une sortie débranchée reste visible mais inutilisable : la masquer
            // ferait disparaître un bouton sous le doigt.
            disabled={!usable || !output.connected}
            onClick={() => onPick(output.id)}
            className={`min-h-11 flex-1 rounded-lg border px-4 transition-colors disabled:opacity-40 ${
              active
                ? 'border-neutral-500 bg-neutral-800 text-neutral-100'
                : 'border-neutral-800 bg-neutral-900 text-neutral-300 active:bg-neutral-800'
            }`}
          >
            {output.name}
            {!output.connected && <span className="block text-xs text-neutral-600">débranchée</span>}
          </button>
        );
      })}
    </div>
  );
}

function VolumeRow({
  muted,
  usable,
  onVolume,
  onMute,
}: {
  muted: boolean;
  usable: boolean;
  onVolume: (level: number) => void;
  onMute: () => void;
}) {
  const volume = useRoom((s) => s.audio?.volume ?? 0);

  // Le volume est la seule commande qui échappe à la règle « pas de mise à jour
  // optimiste », et pour une raison précise : un curseur qui ne suit pas le doigt
  // est inutilisable. On affiche donc la valeur saisie, et on repasse à la valeur
  // serveur dès qu'elle la rejoint. En cas d'échec, le curseur revient tout seul
  // au prochain état poussé par l'agent.
  const [pending, setPending] = useState<number | null>(null);

  useEffect(() => {
    if (pending !== null && volume === pending) {
      setPending(null);
    }
  }, [volume, pending]);

  const shown = pending ?? volume;

  function commit(level: number) {
    setPending(level);
    onVolume(level);
  }

  return (
    <div className="flex items-center gap-4">
      <button
        type="button"
        disabled={!usable}
        onClick={onMute}
        className="min-h-11 min-w-16 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-sm text-neutral-300 disabled:opacity-40"
      >
        {muted ? 'Muet' : 'Son'}
      </button>

      <input
        type="range"
        min={0}
        max={100}
        step={1}
        value={shown}
        disabled={!usable}
        // Glissement : affichage local uniquement, aucune requête par pixel parcouru.
        onChange={(event) => setPending(Number(event.currentTarget.value))}
        // Envoi au relâchement seulement.
        onPointerUp={(event) => commit(Number(event.currentTarget.value))}
        onKeyUp={(event) => commit(Number(event.currentTarget.value))}
        className="h-11 flex-1 accent-neutral-300 disabled:opacity-40"
      />

      <span className="w-12 text-right text-sm tabular-nums text-neutral-300">
        {muted ? '—' : `${shown} %`}
      </span>
    </div>
  );
}

const EMPTY_OUTPUTS: never[] = [];
