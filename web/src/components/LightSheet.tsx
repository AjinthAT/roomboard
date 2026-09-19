import { useEffect, useState } from 'react';
import { identifyLight } from '../api/client';
import type { LightCommand } from '../api/client';
import type { LightSnapshot } from '../api/types';
import { ColorWheel } from './ColorWheel';
import { useThrottledSend } from './useThrottledSend';

type Tab = 'color' | 'white' | 'effects';

/**
 * Vue détaillée d'une lampe, en plein écran.
 *
 * Tout ce que l'ampoule sait faire tient ici, et la carte reste une ligne. Un panneau
 * mural se lit en moins de deux secondes (docs/07-frontend.md) : entasser roue,
 * températures, vingt effets et réglages d'alimentation dans la liste aurait rendu
 * l'écran d'accueil illisible. On ouvre quand on veut régler, on ferme après.
 */
export function LightSheet({
  light,
  usable,
  onSend,
  onError,
  onClose,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
  onError: (message: string) => void;
  onClose: () => void;
}) {
  const caps = light.capabilities;
  const [tab, setTab] = useState<Tab>(caps.color ? 'color' : 'white');

  // Échap ferme, comme n'importe quelle fenêtre. Utile au clavier et sur un Mac.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const tabs: ReadonlyArray<{ id: Tab; label: string; shown: boolean }> = [
    { id: 'color', label: 'Couleur', shown: caps.color },
    { id: 'white', label: 'Blanc', shown: caps.colorTemp },
    { id: 'effects', label: 'Effets', shown: caps.effects.length > 0 },
  ];

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={light.name}
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/70 p-0 sm:items-center sm:p-6"
      onPointerDown={(e) => {
        if (e.target === e.currentTarget) {
          onClose();
        }
      }}
    >
      <div className="flex max-h-full w-full max-w-md flex-col gap-5 overflow-y-auto rounded-t-3xl border border-neutral-800 bg-neutral-950 p-4 pb-[max(1rem,env(safe-area-inset-bottom))] sm:rounded-3xl sm:p-6">
        <header className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <span
              className="size-4 rounded-full"
              style={{ background: light.on ? (light.colorHex ?? '#e8e8ea') : '#2a2a2e' }}
            />
            <h2 className="text-lg text-neutral-100">{light.name}</h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Fermer"
            className="min-h-11 min-w-11 rounded-lg border border-neutral-800 text-neutral-400 active:bg-neutral-900"
          >
            ✕
          </button>
        </header>

        <button
          type="button"
          disabled={!usable}
          onClick={() => onSend(light.id, { on: !light.on, transitionSec: 0.4 })}
          className={`min-h-12 rounded-xl border text-base transition-colors disabled:opacity-40 ${
            light.on
              ? 'border-neutral-500 bg-neutral-800 text-neutral-100'
              : 'border-neutral-800 bg-neutral-900 text-neutral-400'
          }`}
        >
          {light.on ? 'Allumée' : 'Éteinte'}
        </button>

        {caps.brightness && <Brightness light={light} usable={usable} onSend={onSend} />}

        {tabs.some((t) => t.shown) && (
          <>
            <div className="flex gap-2 rounded-xl border border-neutral-900 bg-neutral-900/40 p-1">
              {tabs.filter((t) => t.shown).map((t) => (
                <button
                  key={t.id}
                  type="button"
                  onClick={() => setTab(t.id)}
                  className={`min-h-10 flex-1 rounded-lg text-sm transition-colors ${
                    tab === t.id ? 'bg-neutral-800 text-neutral-100' : 'text-neutral-500'
                  }`}
                >
                  {t.label}
                </button>
              ))}
            </div>

            {tab === 'color' && caps.color && (
              <div className="flex flex-col items-center gap-2 py-1">
                <ColorWheel
                  value={light.colorHex}
                  disabled={!usable}
                  onChange={(hex) => onSend(light.id, { colorHex: hex, on: true, transitionSec: 0.12 })}
                />
                <span className="text-xs tabular-nums text-neutral-600">
                  {light.colorHex ?? 'couleur inconnue'}
                </span>
              </div>
            )}

            {tab === 'white' && caps.colorTemp && (
              <White light={light} usable={usable} onSend={onSend} />
            )}

            {tab === 'effects' && <Effects light={light} usable={usable} onSend={onSend} />}
          </>
        )}

        <Advanced light={light} usable={usable} onSend={onSend} onError={onError} />
      </div>
    </div>
  );
}

function Brightness({
  light,
  usable,
  onSend,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  const [pending, setPending] = useState<number | null>(null);
  const serverValue = light.brightness ?? 0;

  const throttled = useThrottledSend<number>((level) =>
    onSend(light.id, { brightness: level, on: level > 0, transitionSec: 0.12 }));

  useEffect(() => {
    if (pending !== null && serverValue === pending) {
      setPending(null);
    }
  }, [serverValue, pending]);

  const shown = pending ?? serverValue;

  return (
    <div className="flex items-center gap-4">
      <span className="w-20 shrink-0 text-xs uppercase tracking-wider text-neutral-600">
        Luminosité
      </span>
      <input
        type="range"
        min={0}
        max={100}
        value={shown}
        disabled={!usable}
        aria-label="Luminosité"
        onChange={(e) => {
          const level = Number(e.currentTarget.value);
          setPending(level);
          throttled.push(level);
        }}
        onPointerUp={(e) => throttled.commit(Number(e.currentTarget.value))}
        onKeyUp={(e) => throttled.commit(Number(e.currentTarget.value))}
        className="h-11 flex-1 accent-neutral-300 disabled:opacity-40"
      />
      <span className="w-12 text-right text-sm tabular-nums text-neutral-400">{shown} %</span>
    </div>
  );
}

/**
 * Blancs : un curseur continu, plus quatre repères.
 *
 * La température est reproduite exactement par l'ampoule, là où une couleur RVB
 * dérive dans son gamut — c'est pourquoi les blancs ont leur onglet et ne sont pas
 * sur la roue.
 */
function White({
  light,
  usable,
  onSend,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  const min = light.capabilities.colorTempMin ?? 154;
  const max = light.capabilities.colorTempMax ?? 455;
  const [pending, setPending] = useState<number | null>(null);
  const serverValue = light.colorTempMired ?? Math.round((min + max) / 2);

  const throttled = useThrottledSend<number>((mired) =>
    onSend(light.id, { colorTempMired: mired, on: true, transitionSec: 0.12 }));

  useEffect(() => {
    if (pending !== null && serverValue === pending) {
      setPending(null);
    }
  }, [serverValue, pending]);

  const shown = pending ?? serverValue;
  const presets = [
    { mired: min, label: 'Très froid' },
    { mired: 250, label: 'Froid' },
    { mired: 370, label: 'Neutre' },
    { mired: max, label: 'Chaud' },
  ].filter((p) => p.mired >= min && p.mired <= max);

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-4">
        <input
          type="range"
          min={min}
          max={max}
          value={shown}
          disabled={!usable}
          aria-label="Température de blanc"
          onChange={(e) => {
            const mired = Number(e.currentTarget.value);
            setPending(mired);
            throttled.push(mired);
          }}
          onPointerUp={(e) => throttled.commit(Number(e.currentTarget.value))}
          onKeyUp={(e) => throttled.commit(Number(e.currentTarget.value))}
          className="h-11 flex-1 rounded-lg"
          style={{
            accentColor: '#e8e8ea',
            background: 'linear-gradient(to right, #cfe3ff, #ffffff, #ffc27a)',
          }}
        />
        <span className="w-16 text-right text-sm tabular-nums text-neutral-400">
          {Math.round(1_000_000 / shown)} K
        </span>
      </div>

      <div className="flex flex-wrap gap-2">
        {presets.map((p) => (
          <button
            key={p.label}
            type="button"
            disabled={!usable}
            onClick={() => {
              setPending(p.mired);
              throttled.commit(p.mired);
            }}
            className="min-h-10 flex-1 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-xs text-neutral-400 active:bg-neutral-800 disabled:opacity-40"
          >
            {p.label}
          </button>
        ))}
      </div>
    </div>
  );
}

/**
 * Les commandes de cycle de vie ne sont pas des ambiances : elles n'ont rien à
 * faire dans une grille de choix, et « Arrêter » les remplace toutes.
 */
export function ambienceEffects(effects: readonly string[]): string[] {
  const control = new Set([
    'none', 'finish_effect', 'stop_effect', 'stop_hue_effect', 'okay', 'channel_change',
  ]);

  return effects.filter((e) => !control.has(e));
}

export const EFFECT_LABELS: Record<string, string> = {
  colorloop: 'Boucle de couleurs',
  candle: 'Bougie',
  fireplace: 'Cheminée',
  sunrise: 'Lever de soleil',
  sunset: 'Coucher de soleil',
  sparkle: 'Étincelles',
  glisten: 'Scintillement',
  opal: 'Opale',
  underwater: 'Sous-marin',
  cosmos: 'Cosmos',
  sunbeam: 'Rayon',
  enchant: 'Enchanté',
  breathe: 'Respiration',
  blink: 'Clignotement',
};

function Effects({
  light,
  usable,
  onSend,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  const ambiences = ambienceEffects(light.capabilities.effects);

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-2">
        {ambiences.map((effect) => (
          <button
            key={effect}
            type="button"
            disabled={!usable}
            onClick={() => onSend(light.id, { effect, on: true })}
            className="min-h-11 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-sm text-neutral-300 active:bg-neutral-800 disabled:opacity-40"
          >
            {EFFECT_LABELS[effect] ?? effect}
          </button>
        ))}
      </div>

      <button
        type="button"
        disabled={!usable}
        onClick={() => onSend(light.id, { effect: 'stop_effect' })}
        className="min-h-11 rounded-lg border border-neutral-700 text-sm text-neutral-300 active:bg-neutral-800 disabled:opacity-40"
      >
        Arrêter l'effet
      </button>
    </div>
  );
}

function Advanced({
  light,
  usable,
  onSend,
  onError,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
  onError: (message: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const labels: Record<string, string> = {
    off: 'Éteinte', on: 'Allumée', toggle: 'Inverser', previous: 'État précédent',
  };

  return (
    <div className="flex flex-col gap-3 border-t border-neutral-900 pt-4">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex items-center justify-between text-xs uppercase tracking-wider text-neutral-600"
      >
        <span>Réglages</span>
        <span>{open ? '−' : '+'}</span>
      </button>

      {open && (
        <>
          {light.capabilities.powerOnBehaviours.length > 0 && (
            <div className="flex flex-col gap-2">
              <span className="text-xs text-neutral-600">Au rallumage mural</span>
              <div className="flex flex-wrap gap-2">
                {light.capabilities.powerOnBehaviours.map((value) => (
                  <button
                    key={value}
                    type="button"
                    disabled={!usable}
                    onClick={() => onSend(light.id, { powerOnBehavior: value })}
                    className={`min-h-10 rounded-lg border px-3 text-xs transition-colors disabled:opacity-40 ${
                      light.powerOnBehavior === value
                        ? 'border-neutral-500 bg-neutral-800 text-neutral-200'
                        : 'border-neutral-800 bg-neutral-900 text-neutral-400'
                    }`}
                  >
                    {labels[value] ?? value}
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="flex items-center justify-between gap-3">
            <span className="text-xs text-neutral-600">
              {light.linkQuality !== null ? `Signal ${light.linkQuality}/255` : 'Signal inconnu'}
            </span>
            <button
              type="button"
              disabled={!usable}
              onClick={() => {
                identifyLight(light.id).catch((cause: unknown) =>
                  onError(cause instanceof Error ? cause.message : 'Échec'));
              }}
              className="min-h-10 rounded-lg border border-neutral-800 px-3 text-xs text-neutral-400 active:bg-neutral-800 disabled:opacity-40"
            >
              Faire clignoter
            </button>
          </div>
        </>
      )}
    </div>
  );
}
