import { useState } from 'react';
import { UnauthorizedError, runScene } from '../api/client';
import { useRoom } from '../store/roomStore';
import { SceneEditor } from './SceneEditor';
import { useConfirm } from './useConfirm';

export function SceneBar({ onUnauthorized }: { onUnauthorized: () => void }) {
  const scenes = useRoom((s) => s.scenes);
  const live = useRoom((s) => s.connection === 'connected');
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  // `null` = création, une chaîne = la scène ouverte, `undefined` = éditeur fermé.
  const [edited, setEdited] = useState<string | null | undefined>(undefined);

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
      <div className="flex items-center justify-between">
        <h2 className="text-xs font-medium uppercase tracking-[0.2em] text-neutral-500">Scènes</h2>
        <button
          type="button"
          onClick={() => setEditing((v) => !v)}
          aria-pressed={editing}
          className={`min-h-9 rounded-lg border px-3 text-xs ${
            editing
              ? 'border-neutral-600 bg-neutral-800 text-neutral-200'
              : 'border-neutral-800 text-neutral-500 active:bg-neutral-900'
          }`}
        >
          {editing ? 'Terminé' : 'Modifier'}
        </button>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {scenes.map((scene) => (
          <SceneButton
            key={scene.id}
            id={scene.id}
            name={scene.name}
            destructive={scene.destructive}
            live={live}
            editing={editing}
            onRun={launch}
            onEdit={setEdited}
          />
        ))}

        {editing && (
          <button
            type="button"
            onClick={() => setEdited(null)}
            className="min-h-14 rounded-xl border border-dashed border-neutral-700 px-4 text-base text-neutral-400 active:bg-neutral-900"
          >
            + Nouvelle
          </button>
        )}
      </div>

      <SceneProgress />

      {error && <p className="text-xs text-red-400">{error}</p>}

      {edited !== undefined && (
        <SceneEditor
          sceneId={edited}
          onError={setError}
          onClose={() => setEdited(undefined)}
          onSaved={() => setError(null)}
        />
      )}
    </section>
  );
}

function SceneButton({
  id,
  name,
  destructive,
  live,
  editing,
  onRun,
  onEdit,
}: {
  id: string;
  name: string;
  destructive: boolean;
  live: boolean;
  editing: boolean;
  onRun: (id: string) => void;
  onEdit: (id: string) => void;
}) {
  const running = useRoom((s) => s.sceneRun?.sceneId === id && s.sceneRun.status === 'running');
  const { armed, arm, disarm } = useConfirm();

  // Night éteint le PC. Le caractère sensible vient du serveur, qui le déduit des
  // étapes : aucune scène n'est traitée à part par son nom.
  function handle() {
    if (editing) {
      onEdit(id);
      return;
    }

    if (destructive && !armed) {
      arm();
      return;
    }

    disarm();
    onRun(id);
  }

  return (
    <button
      type="button"
      disabled={!live && !editing}
      onClick={handle}
      className={`min-h-14 rounded-xl border px-4 text-base transition-colors disabled:opacity-40 ${
        editing
          ? 'border-dashed border-neutral-600 bg-neutral-900 text-neutral-300'
          : armed
            ? 'border-amber-600 bg-amber-950/40 text-amber-200'
            : running
              ? 'border-neutral-500 bg-neutral-800 text-neutral-100'
              : 'border-neutral-800 bg-neutral-900 text-neutral-300 active:bg-neutral-800'
      }`}
    >
      {armed ? 'Confirmer ?' : name}
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
