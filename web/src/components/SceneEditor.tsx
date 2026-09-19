import { useEffect, useState } from 'react';
import { createScene, deleteScene, getScene, updateScene } from '../api/client';
import type { SceneStep } from '../api/types';
import { useRoom } from '../store/roomStore';
import { EFFECT_LABELS, ambienceEffects } from './LightSheet';

/**
 * Description des types d'étapes, pilotée par données.
 *
 * L'écran se construit à partir de cette table plutôt que d'une suite de conditions :
 * ajouter un type au DSL ne demande alors qu'une ligne ici. C'est la seule façon de
 * garder un éditeur de onze types lisible.
 */
type FieldKind = 'pc' | 'light' | 'output' | 'percent' | 'onoff' | 'colour' | 'mired'
  | 'effect' | 'millis' | 'seconds' | 'uri' | 'wait';

const NO_OUTPUTS: never[] = [];

const STEP_TYPES: ReadonlyArray<{ type: string; label: string; fields: FieldKind[] }> = [
  { type: 'pc.wake', label: 'Allumer le PC', fields: ['pc', 'wait'] },
  { type: 'pc.shutdown', label: 'Éteindre le PC', fields: ['pc'] },
  { type: 'audio.setOutput', label: 'Choisir la sortie audio', fields: ['pc', 'output'] },
  { type: 'audio.setVolume', label: 'Volume Windows', fields: ['pc', 'percent'] },
  { type: 'audio.setMute', label: 'Couper le son', fields: ['pc', 'onoff'] },
  { type: 'light.set', label: 'Régler une lampe', fields: ['light', 'onoff', 'percent', 'colour', 'mired', 'effect', 'seconds'] },
  { type: 'music.transfer', label: 'Basculer la musique sur le PC', fields: [] },
  { type: 'music.play', label: 'Lancer la musique', fields: ['uri'] },
  { type: 'music.pause', label: 'Mettre la musique en pause', fields: [] },
  { type: 'music.setVolume', label: 'Volume Spotify', fields: ['percent'] },
  { type: 'delay', label: 'Attendre', fields: ['millis'] },
];

export function SceneEditor({
  sceneId,
  onError,
  onClose,
  onSaved,
}: {
  /** `null` pour créer une scène. */
  sceneId: string | null;
  onError: (message: string) => void;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState('');
  const [steps, setSteps] = useState<SceneStep[]>([]);
  const [loading, setLoading] = useState(sceneId !== null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (sceneId === null) {
      return;
    }

    const controller = new AbortController();

    getScene(sceneId, controller.signal)
      .then((scene) => {
        setName(scene.name);
        setSteps(scene.steps);
      })
      .catch((cause: unknown) =>
        onError(cause instanceof Error ? cause.message : 'Lecture impossible'))
      .finally(() => setLoading(false));

    return () => controller.abort();
  }, [sceneId, onError]);

  async function save() {
    if (!name.trim()) {
      onError('Une scène a besoin d’un nom.');
      return;
    }

    setSaving(true);

    try {
      if (sceneId === null) {
        await createScene({ name: name.trim(), steps });
      } else {
        await updateScene(sceneId, { name: name.trim(), steps });
      }
      onSaved();
      onClose();
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'Enregistrement impossible');
    } finally {
      setSaving(false);
    }
  }

  async function remove() {
    if (sceneId === null) {
      return;
    }

    try {
      await deleteScene(sceneId);
      onSaved();
      onClose();
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'Suppression impossible');
    }
  }

  function patchStep(index: number, patch: Partial<SceneStep>) {
    setSteps((prev) => prev.map((s, i) => (i === index ? { ...s, ...patch } : s)));
  }

  function move(index: number, delta: number) {
    const target = index + delta;

    if (target < 0 || target >= steps.length) {
      return;
    }

    setSteps((prev) => {
      const next = [...prev];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={sceneId === null ? 'Nouvelle scène' : 'Modifier la scène'}
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/70 sm:items-center sm:p-6"
      onPointerDown={(e) => {
        if (e.target === e.currentTarget) {
          onClose();
        }
      }}
    >
      <div className="flex max-h-full w-full max-w-lg flex-col gap-4 overflow-y-auto rounded-t-3xl border border-neutral-800 bg-neutral-950 p-4 pb-[max(1rem,env(safe-area-inset-bottom))] sm:rounded-3xl sm:p-6">
        <header className="flex items-center justify-between gap-3">
          <input
            value={name}
            placeholder="Nom de la scène"
            onChange={(e) => setName(e.currentTarget.value)}
            className="min-h-11 min-w-0 flex-1 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-lg text-neutral-100 outline-none focus:border-neutral-600"
          />
          <button
            type="button"
            onClick={onClose}
            aria-label="Fermer"
            className="min-h-11 min-w-11 shrink-0 rounded-lg border border-neutral-800 text-neutral-400 active:bg-neutral-900"
          >
            ✕
          </button>
        </header>

        {loading ? (
          <p className="py-6 text-center text-sm text-neutral-600">Chargement…</p>
        ) : (
          <>
            <ol className="flex flex-col gap-3">
              {steps.map((step, index) => (
                <StepRow
                  key={index}
                  index={index}
                  step={step}
                  total={steps.length}
                  onPatch={(patch) => patchStep(index, patch)}
                  onMove={(delta) => move(index, delta)}
                  onRemove={() => setSteps((prev) => prev.filter((_, i) => i !== index))}
                />
              ))}
            </ol>

            <AddStep onAdd={(step) => setSteps((prev) => [...prev, step])} />

            <div className="flex gap-3 border-t border-neutral-900 pt-4">
              {sceneId !== null && (
                <button
                  type="button"
                  onClick={remove}
                  className="min-h-11 rounded-lg border border-red-900/60 px-4 text-sm text-red-300 active:bg-red-950/40"
                >
                  Supprimer
                </button>
              )}
              <button
                type="button"
                disabled={saving}
                onClick={save}
                className="min-h-11 flex-1 rounded-lg bg-neutral-100 text-base font-medium text-neutral-900 active:bg-neutral-300 disabled:opacity-40"
              >
                {saving ? 'Enregistrement…' : 'Enregistrer'}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function StepRow({
  index,
  step,
  total,
  onPatch,
  onMove,
  onRemove,
}: {
  index: number;
  step: SceneStep;
  total: number;
  onPatch: (patch: Partial<SceneStep>) => void;
  onMove: (delta: number) => void;
  onRemove: () => void;
}) {
  const spec = STEP_TYPES.find((t) => t.type === step.type);

  return (
    <li className="flex flex-col gap-3 rounded-xl border border-neutral-900 bg-neutral-900/30 p-3">
      <div className="flex items-center gap-2">
        <span className="w-6 shrink-0 text-center text-xs text-neutral-700">{index + 1}</span>
        <span className="flex-1 truncate text-sm text-neutral-300">
          {spec?.label ?? step.type}
        </span>
        <button type="button" onClick={() => onMove(-1)} disabled={index === 0}
          aria-label="Monter"
          className="min-h-9 min-w-9 rounded border border-neutral-800 text-neutral-500 disabled:opacity-30">↑</button>
        <button type="button" onClick={() => onMove(1)} disabled={index === total - 1}
          aria-label="Descendre"
          className="min-h-9 min-w-9 rounded border border-neutral-800 text-neutral-500 disabled:opacity-30">↓</button>
        <button type="button" onClick={onRemove}
          aria-label="Retirer l'étape"
          className="min-h-9 min-w-9 rounded border border-neutral-800 text-neutral-500 active:bg-neutral-800">✕</button>
      </div>

      {spec && spec.fields.length > 0 && (
        <div className="flex flex-wrap gap-2 pl-8">
          {spec.fields.map((field) => (
            <Field key={field} kind={field} step={step} onPatch={onPatch} />
          ))}
        </div>
      )}
    </li>
  );
}

function Field({
  kind,
  step,
  onPatch,
}: {
  kind: FieldKind;
  step: SceneStep;
  onPatch: (patch: Partial<SceneStep>) => void;
}) {
  // Sélecteurs renvoyant la référence telle qu'elle est dans le store : construire
  // un tableau ici en ferait un objet neuf à chaque rendu, et `useSyncExternalStore`
  // compare par identité — la boucle de rendu serait infinie.
  const pc = useRoom((s) => s.pc);
  const lights = useRoom((s) => s.lights);
  const audio = useRoom((s) => s.audio);
  const outputs = audio?.outputs ?? NO_OUTPUTS;

  const input = 'min-h-11 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-sm text-neutral-200';

  switch (kind) {
    case 'pc':
      return (
        <select className={input} value={step.deviceId ?? ''}
          onChange={(e) => onPatch({ deviceId: e.currentTarget.value })}>
          <option value="">PC…</option>
          {pc && <option value={pc.id}>{pc.name}</option>}
        </select>
      );

    case 'light':
      return (
        <select className={input} value={step.deviceId ?? ''}
          onChange={(e) => onPatch({ deviceId: e.currentTarget.value })}>
          <option value="">Lampe…</option>
          {lights.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
        </select>
      );

    case 'output':
      return (
        <select className={input} value={step.outputId ?? ''}
          onChange={(e) => onPatch({ outputId: e.currentTarget.value })}>
          <option value="">Sortie…</option>
          {outputs.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
        </select>
      );

    case 'onoff': {
      // `audio.setMute` lit `muted`, `light.set` lit `on` : un seul bouton, deux champs.
      const muting = step.type === 'audio.setMute';
      const value = muting ? step.muted === true : step.on === true;
      const labels = muting ? ['Couper le son', 'Rétablir le son'] : ['Allumer', 'Éteindre'];

      return (
        <button type="button"
          onClick={() => onPatch(muting ? { muted: !value } : { on: !value })}
          className={`${input} ${value ? 'text-neutral-100' : 'text-neutral-500'}`}>
          {value ? labels[0] : labels[1]}
        </button>
      );
    }

    case 'percent':
      return (
        <label className="flex items-center gap-2">
          <input type="number" min={0} max={100} className={`${input} w-20`}
            value={step.level ?? step.brightness ?? ''}
            placeholder="%"
            onChange={(e) => {
              const v = e.currentTarget.value === '' ? null : Number(e.currentTarget.value);
              onPatch(step.type === 'light.set' ? { brightness: v } : { level: v });
            }} />
          <span className="text-xs text-neutral-600">%</span>
        </label>
      );

    case 'colour':
      return (
        <input type="text" className={`${input} w-28`} value={step.colorHex ?? ''}
          placeholder="#FF4400"
          onChange={(e) => onPatch({ colorHex: e.currentTarget.value || null })} />
      );

    case 'mired':
      return (
        <label className="flex items-center gap-2">
          <input type="number" min={154} max={455} className={`${input} w-24`}
            value={step.colorTempMired ?? ''} placeholder="blanc"
            onChange={(e) => onPatch({
              colorTempMired: e.currentTarget.value === '' ? null : Number(e.currentTarget.value),
            })} />
          <span className="text-xs text-neutral-600">mired</span>
        </label>
      );

    case 'effect': {
      const light = lights.find((l) => l.id === step.deviceId);
      const ambiences = light ? ambienceEffects(light.capabilities.effects) : [];

      if (ambiences.length === 0) {
        return null;
      }

      return (
        <select className={input} value={step.effect ?? ''}
          onChange={(e) => onPatch({ effect: e.currentTarget.value || null })}>
          <option value="">Sans effet</option>
          {ambiences.map((eff) => (
            <option key={eff} value={eff}>{EFFECT_LABELS[eff] ?? eff}</option>
          ))}
        </select>
      );
    }

    case 'seconds':
      return (
        <label className="flex items-center gap-2">
          <input type="number" min={0} max={60} step={0.5} className={`${input} w-20`}
            value={step.transitionSec ?? ''} placeholder="fondu"
            onChange={(e) => onPatch({
              transitionSec: e.currentTarget.value === '' ? null : Number(e.currentTarget.value),
            })} />
          <span className="text-xs text-neutral-600">s de fondu</span>
        </label>
      );

    case 'millis':
      return (
        <label className="flex items-center gap-2">
          <input type="number" min={0} max={10000} step={100} className={`${input} w-24`}
            value={step.ms ?? ''} placeholder="1000"
            onChange={(e) => onPatch({ ms: Number(e.currentTarget.value) })} />
          <span className="text-xs text-neutral-600">ms</span>
        </label>
      );

    case 'uri':
      return (
        <input type="text" className={`${input} flex-1`} value={step.uri ?? ''}
          placeholder="spotify:playlist:… (facultatif)"
          onChange={(e) => onPatch({ uri: e.currentTarget.value || null })} />
      );

    case 'wait':
      return (
        <button type="button"
          onClick={() => onPatch({ waitForOnline: !step.waitForOnline })}
          className={`${input} ${step.waitForOnline ? 'text-neutral-100' : 'text-neutral-500'}`}>
          {step.waitForOnline ? 'Attendre le démarrage' : 'Ne pas attendre'}
        </button>
      );
  }
}

function AddStep({ onAdd }: { onAdd: (step: SceneStep) => void }) {
  const [picking, setPicking] = useState(false);

  if (!picking) {
    return (
      <button type="button" onClick={() => setPicking(true)}
        className="min-h-11 rounded-lg border border-dashed border-neutral-800 text-sm text-neutral-500 active:bg-neutral-900">
        + Ajouter une étape
      </button>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-2 rounded-xl border border-neutral-900 bg-neutral-900/30 p-3 sm:grid-cols-2">
      {STEP_TYPES.map((t) => (
        <button key={t.type} type="button"
          onClick={() => {
            // Valeurs de départ raisonnables : une étape ajoutée doit être
            // exécutable sans qu'on ait à remplir six champs.
            onAdd({
              type: t.type,
              on: t.type === 'light.set' ? true : undefined,
              muted: t.type === 'audio.setMute' ? true : undefined,
              waitForOnline: t.type === 'pc.wake' ? true : undefined,
              ms: t.type === 'delay' ? 1000 : undefined,
            });
            setPicking(false);
          }}
          className="min-h-11 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-left text-sm text-neutral-300 active:bg-neutral-800">
          {t.label}
        </button>
      ))}
    </div>
  );
}
