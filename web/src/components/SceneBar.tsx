import { useState } from 'react';
import { UnauthorizedError, runScene } from '../api/client';
import { useRoom } from '../store/roomStore';

export function SceneBar({ onUnauthorized }: { onUnauthorized: () => void }) {
  const scenes = useRoom((s) => s.scenes);
  const live = useRoom((s) => s.connection === 'connected');
  const [error, setError] = useState<string | null>(null);

  if (scenes.length === 0) {
    return null;
  }

  async function launch(id: string) {
    setError(null);
    try {
      await runScene(id);
    } catch (cause) {
      if (cause instanceof UnauthorizedError) {
        onUnauthorized();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Échec');
    }
  }

  return (
    <section className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {scenes.map((scene) => (
          <SceneButton key={scene.id} id={scene.id} name={scene.name} live={live} onRun={launch} />
        ))}
      </div>

      <SceneProgress />

      {error && <p className="text-xs text-red-400">{error}</p>}
    </section>
  );
}

function SceneButton({
  id,
  name,
  live,
  onRun,
}: {
  id: string;
  name: string;
  live: boolean;
  onRun: (id: string) => void;
}) {
  const running = useRoom((s) => s.sceneRun?.sceneId === id && s.sceneRun.status === 'running');

  return (
    <button
      type="button"
      disabled={!live}
      onClick={() => onRun(id)}
      className={`min-h-14 rounded-xl border px-4 text-base transition-colors disabled:opacity-40 ${
        running
          ? 'border-neutral-500 bg-neutral-800 text-neutral-100'
          : 'border-neutral-800 bg-neutral-900 text-neutral-300 active:bg-neutral-800'
      }`}
    >
      {name}
    </button>
  );
}

/**
 * Progression réelle, pas un minuteur : elle avance à chaque événement
 * SceneStepCompleted. Une scène qui attend le réveil d'un PC peut durer une minute
 * et demie, et l'écran doit montrer qu'il se passe quelque chose (docs/07-frontend.md).
 */
function SceneProgress() {
  const run = useRoom((s) => s.sceneRun);
  const sceneName = useRoom((s) =>
    s.scenes.find((scene) => scene.id === s.sceneRun?.sceneId)?.name ?? '');

  if (!run) {
    return null;
  }

  const label =
    run.status === 'running' ? `${sceneName} en cours — ${run.done} étape${run.done > 1 ? 's' : ''}`
    : run.status === 'completed' && run.failed === 0 ? `${sceneName} appliquée`
    : run.status === 'completed' ? `${sceneName} appliquée, ${run.failed} étape${run.failed > 1 ? 's' : ''} en échec`
    : run.status === 'cancelled' ? `${sceneName} interrompue`
    : `${sceneName} a échoué`;

  const tone =
    run.status === 'running' ? 'text-neutral-400'
    : run.failed > 0 || run.status === 'failed' ? 'text-amber-400'
    : 'text-neutral-500';

  return <p className={`text-sm ${tone}`}>{label}</p>;
}
