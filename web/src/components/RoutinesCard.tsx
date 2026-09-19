import { useState } from 'react';
import { UnauthorizedError, createRoutine, deleteRoutine, saveRoutine } from '../api/client';
import type { RoutinePatch } from '../api/client';
import type { RoutineInfo } from '../api/types';
import { roomStore, useRoom } from '../store/roomStore';

const DAYS = ['L', 'M', 'M', 'J', 'V', 'S', 'D'];

/**
 * Routines : une scène lancée à une heure donnée.
 *
 * Elles n'exécutent rien elles-mêmes, elles déclenchent le moteur de scènes. Tout ce
 * qui a été éprouvé sur les scènes vaut donc pour elles, et l'écran n'a qu'à régler
 * quand, pas quoi.
 */
export function RoutinesCard({ onUnauthorized }: { onUnauthorized: () => void }) {
  const routines = useRoom((s) => s.routines);
  const scenes = useRoom((s) => s.scenes);
  const live = useRoom((s) => s.connection === 'connected');
  const [error, setError] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);

  async function save(id: string, patch: RoutinePatch) {
    setError(null);
    // L'affichage suit immédiatement : une case à cocher qui attend le réseau donne
    // l'impression de ne pas avoir été touchée.
    roomStore.patchRoutine(id, patch as Partial<RoutineInfo>);

    try {
      await saveRoutine(id, patch);
    } catch (cause) {
      if (cause instanceof UnauthorizedError) {
        onUnauthorized();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Échec');
    }
  }

  async function remove(id: string) {
    setError(null);

    try {
      await deleteRoutine(id);
    } catch (cause) {
      if (cause instanceof UnauthorizedError) {
        onUnauthorized();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Échec');
    }
  }

  return (
    <section className="flex flex-col gap-4 rounded-2xl border border-neutral-800 bg-neutral-950 p-6">
      <div className="flex items-center justify-between">
        <h2 className="text-xs font-medium uppercase tracking-[0.2em] text-neutral-500">Routines</h2>
        <button
          type="button"
          disabled={scenes.length === 0}
          onClick={() => setAdding((v) => !v)}
          aria-expanded={adding}
          className="min-h-9 rounded-lg border border-neutral-800 px-3 text-xs text-neutral-500 active:bg-neutral-900 disabled:opacity-40"
        >
          {adding ? 'Annuler' : '+ Ajouter'}
        </button>
      </div>

      {adding && (
        <NewRoutine
          onCancel={() => setAdding(false)}
          onError={setError}
          onCreated={() => setAdding(false)}
        />
      )}

      {routines.length === 0 && !adding && (
        <p className="text-sm text-neutral-600">Aucune routine.</p>
      )}

      {routines.map((routine) => (
        <RoutineRow
          key={routine.id}
          routine={routine}
          live={live}
          onSave={save}
          onRemove={remove}
        />
      ))}

      {error && <p className="text-xs text-red-400">{error}</p>}
    </section>
  );
}

/**
 * Création : nom, scène, heure. Les jours et l'activation se règlent ensuite dans la
 * ligne dépliée — le serveur crée la routine désactivée, on la relit avant qu'elle parte.
 */
function NewRoutine({
  onCancel,
  onError,
  onCreated,
}: {
  onCancel: () => void;
  onError: (message: string) => void;
  onCreated: () => void;
}) {
  const scenes = useRoom((s) => s.scenes);
  const [name, setName] = useState('');
  const [sceneId, setSceneId] = useState(scenes[0]?.id ?? '');
  const [time, setTime] = useState('07:00');
  const [saving, setSaving] = useState(false);

  async function submit() {
    if (!name.trim()) {
      onError('Une routine a besoin d\u2019un nom.');
      return;
    }

    setSaving(true);

    try {
      await createRoutine({ name: name.trim(), sceneId, time, days: [true, true, true, true, true, true, true] });
      onCreated();
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'Création impossible');
    } finally {
      setSaving(false);
    }
  }

  const field = 'min-h-11 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-sm text-neutral-200';

  return (
    <div className="flex flex-col gap-3 rounded-xl border border-neutral-900 bg-neutral-900/30 p-4">
      <input
        value={name}
        placeholder="Nom de la routine"
        onChange={(e) => setName(e.currentTarget.value)}
        className={field}
      />

      <div className="flex gap-3">
        <select value={sceneId} onChange={(e) => setSceneId(e.currentTarget.value)}
          className={`${field} flex-1`}>
          {scenes.map((scene) => (
            <option key={scene.id} value={scene.id}>{scene.name}</option>
          ))}
        </select>

        <input type="time" value={time} onChange={(e) => setTime(e.currentTarget.value)}
          className={`${field} tabular-nums`} />
      </div>

      <div className="flex gap-3">
        <button type="button" onClick={onCancel}
          className="min-h-11 rounded-lg border border-neutral-800 px-4 text-sm text-neutral-400 active:bg-neutral-900">
          Annuler
        </button>
        <button type="button" disabled={saving || !sceneId} onClick={submit}
          className="min-h-11 flex-1 rounded-lg bg-neutral-100 text-sm font-medium text-neutral-900 active:bg-neutral-300 disabled:opacity-40">
          {saving ? 'Création…' : 'Créer, désactivée'}
        </button>
      </div>
    </div>
  );
}

function RoutineRow({
  routine,
  live,
  onSave,
  onRemove,
}: {
  routine: RoutineInfo;
  live: boolean;
  onSave: (id: string, patch: RoutinePatch) => void;
  onRemove: (id: string) => void;
}) {
  const [open, setOpen] = useState(false);

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <button
          type="button"
          disabled={!live}
          onClick={() => onSave(routine.id, { enabled: !routine.enabled })}
          role="switch"
          aria-checked={routine.enabled}
          aria-label={`${routine.enabled ? 'Désactiver' : 'Activer'} ${routine.name}`}
          className={`h-7 w-12 shrink-0 rounded-full border transition-colors disabled:opacity-40 ${
            routine.enabled ? 'border-emerald-700 bg-emerald-900/60' : 'border-neutral-800 bg-neutral-900'
          }`}
        >
          <span
            className={`block size-5 rounded-full transition-transform ${
              routine.enabled ? 'translate-x-6 bg-emerald-400' : 'translate-x-0.5 bg-neutral-600'
            }`}
          />
        </button>

        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          aria-expanded={open}
          className="flex min-h-11 flex-1 items-center justify-between gap-3 rounded-lg px-1 text-left active:bg-neutral-900"
        >
          <span className="min-w-0">
            <span className="block truncate text-neutral-300">{routine.name}</span>
            <span className="block truncate text-xs text-neutral-600">
              {routine.sceneName} · {summarise(routine.days)}
              {routine.lastFired && ` · déclenchée à ${routine.lastFired}`}
            </span>
          </span>
          <span className="text-xl tabular-nums text-neutral-200">{routine.time}</span>
        </button>
      </div>

      {open && (
        <div className="flex flex-col gap-3 rounded-xl border border-neutral-900 bg-neutral-900/30 p-4">
          <label className="flex items-center justify-between gap-3">
            <span className="text-xs uppercase tracking-wider text-neutral-600">Heure</span>
            <input
              type="time"
              value={routine.time}
              disabled={!live}
              onChange={(e) => onSave(routine.id, { time: e.currentTarget.value })}
              className="min-h-11 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-lg tabular-nums text-neutral-200"
            />
          </label>

          <div className="flex flex-col gap-2">
            <span className="text-xs uppercase tracking-wider text-neutral-600">Jours</span>
            <div className="flex gap-2">
              {DAYS.map((label, index) => (
                <button
                  key={index}
                  type="button"
                  disabled={!live}
                  aria-pressed={routine.days[index]}
                  onClick={() => {
                    const days = [...routine.days];
                    days[index] = !days[index];
                    onSave(routine.id, { days });
                  }}
                  className={`size-11 flex-1 rounded-lg border text-sm transition-colors disabled:opacity-40 ${
                    routine.days[index]
                      ? 'border-neutral-500 bg-neutral-800 text-neutral-100'
                      : 'border-neutral-800 bg-neutral-900 text-neutral-600'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          <SceneChooser routine={routine} live={live} onSave={onSave} />

          <button
            type="button"
            onClick={() => onRemove(routine.id)}
            className="min-h-11 self-start rounded-lg border border-red-900/60 px-4 text-sm text-red-300 active:bg-red-950/40"
          >
            Supprimer la routine
          </button>
        </div>
      )}
    </div>
  );
}

function SceneChooser({
  routine,
  live,
  onSave,
}: {
  routine: RoutineInfo;
  live: boolean;
  onSave: (id: string, patch: RoutinePatch) => void;
}) {
  const scenes = useRoom((s) => s.scenes);

  return (
    <div className="flex flex-col gap-2">
      <span className="text-xs uppercase tracking-wider text-neutral-600">Scène déclenchée</span>
      <div className="flex flex-wrap gap-2">
        {scenes.map((scene) => (
          <button
            key={scene.id}
            type="button"
            disabled={!live}
            onClick={() => onSave(routine.id, { sceneId: scene.id })}
            className={`min-h-10 flex-1 rounded-lg border px-3 text-xs transition-colors disabled:opacity-40 ${
              routine.sceneId === scene.id
                ? 'border-neutral-500 bg-neutral-800 text-neutral-200'
                : 'border-neutral-800 bg-neutral-900 text-neutral-400'
            }`}
          >
            {scene.name}
          </button>
        ))}
      </div>
    </div>
  );
}

function summarise(days: boolean[]): string {
  if (days.every(Boolean)) {
    return 'tous les jours';
  }

  if (days.slice(0, 5).every(Boolean) && !days[5] && !days[6]) {
    return 'en semaine';
  }

  if (!days.slice(0, 5).some(Boolean) && days[5] && days[6]) {
    return 'le week-end';
  }

  const picked = DAYS.filter((_, i) => days[i]);
  return picked.length === 0 ? 'jamais' : picked.join(' ');
}
