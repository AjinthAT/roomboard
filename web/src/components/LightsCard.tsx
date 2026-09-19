import { useEffect, useState } from 'react';
import { UnauthorizedError, identifyLight, setLight } from '../api/client';
import type { LightCommand } from '../api/client';
import type { LightSnapshot } from '../api/types';
import { useRoom } from '../store/roomStore';
import { ColorWheel } from './ColorWheel';
import { useThrottledSend } from './useThrottledSend';

export function LightsCard({ onUnauthorized }: { onUnauthorized: () => void }) {
  const lights = useRoom((s) => s.lights);
  const live = useRoom((s) => s.connection === 'connected');
  const [error, setError] = useState<string | null>(null);

  async function send(id: string, command: LightCommand) {
    setError(null);
    try {
      await setLight(id, command);
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
      <h2 className="text-xs font-medium uppercase tracking-[0.2em] text-neutral-500">Lumières</h2>

      {lights.length === 0 ? (
        <p className="text-sm text-neutral-600">Aucune lampe déclarée.</p>
      ) : (
        lights.map((light) => (
          <LightRow key={light.id} light={light} live={live} onSend={send} onError={setError} />
        ))
      )}

      {error && <p className="text-xs text-red-400">{error}</p>}
    </section>
  );
}

function LightRow({
  light,
  live,
  onSend,
  onError,
}: {
  light: LightSnapshot;
  live: boolean;
  onSend: (id: string, command: LightCommand) => void;
  onError: (message: string) => void;
}) {
  const usable = live && light.reachable;
  const caps = light.capabilities;

  // Les réglages secondaires sont repliés par défaut. Tout reste accessible, mais
  // l'écran doit rester lisible en moins de deux secondes (docs/07-frontend.md).
  const [open, setOpen] = useState(false);
  const hasExtras = caps.colorTemp || caps.effects.length > 0 || caps.powerOnBehaviours.length > 0;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <button
          type="button"
          disabled={!usable}
          onClick={() => onSend(light.id, { on: !light.on, transitionSec: 0.4 })}
          className={`size-11 shrink-0 rounded-lg border transition-colors disabled:opacity-40 ${
            light.on ? 'border-neutral-500 bg-neutral-800' : 'border-neutral-800 bg-neutral-900'
          }`}
          aria-label={light.on ? `Éteindre ${light.name}` : `Allumer ${light.name}`}
        >
          <span
            className="mx-auto block size-3 rounded-full"
            style={{ background: light.on ? (light.colorHex ?? '#e8e8ea') : '#2a2a2e' }}
          />
        </button>

        <span className="flex-1 truncate text-neutral-300">{light.name}</span>

        {!light.paired ? (
          <span className="text-xs text-neutral-600">pas encore appairée</span>
        ) : !light.reachable ? (
          <span className="text-xs text-amber-600/80">injoignable</span>
        ) : null}

        {hasExtras && (
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            aria-expanded={open}
            aria-label="Plus d'options"
            className="min-h-11 rounded-lg border border-neutral-800 px-3 text-xs text-neutral-500 active:bg-neutral-900"
          >
            {open ? '−' : '+'}
          </button>
        )}
      </div>

      {caps.brightness && <BrightnessRow light={light} usable={usable} onSend={onSend} />}
      {caps.color && <ColorRow light={light} usable={usable} onSend={onSend} />}

      {open && (
        <div className="flex flex-col gap-4 rounded-xl border border-neutral-900 bg-neutral-900/30 p-4">
          {caps.colorTemp && <TemperatureRow light={light} usable={usable} onSend={onSend} />}
          {caps.effects.length > 0 && <EffectRow light={light} usable={usable} onSend={onSend} />}
          {caps.powerOnBehaviours.length > 0 && (
            <PowerOnRow light={light} usable={usable} onSend={onSend} />
          )}
          <Diagnostics light={light} usable={usable} onError={onError} />
        </div>
      )}
    </div>
  );
}

function BrightnessRow({
  light,
  usable,
  onSend,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  // La lampe suit le doigt, à cadence limitée. Le fondu est plus court que
  // l'intervalle d'envoi : sinon les commandes se chevauchent et la lampe traîne
  // derrière le geste.
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
    <div className="flex items-center gap-4 pl-14">
      <input
        type="range"
        min={0}
        max={100}
        value={shown}
        disabled={!usable}
        aria-label={`Luminosité ${light.name}`}
        onChange={(event) => {
          const level = Number(event.currentTarget.value);
          setPending(level);
          throttled.push(level);
        }}
        onPointerUp={(event) => throttled.commit(Number(event.currentTarget.value))}
        onKeyUp={(event) => throttled.commit(Number(event.currentTarget.value))}
        className="h-11 flex-1 accent-neutral-300 disabled:opacity-40"
      />
      <span className="w-12 text-right text-sm tabular-nums text-neutral-400">{shown} %</span>
    </div>
  );
}

/**
 * Choix de la couleur à la roue.
 *
 * Les huit pastilles d'origine étaient un compromis pour le tactile, et elles
 * enfermaient l'utilisateur dans un bloc de teintes choisies d'avance. La roue donne
 * tout le cercle, reste une cible large, et suit le doigt : la lampe change pendant
 * le geste, pas seulement au relâchement.
 *
 * Les blancs ne sont pas sur la roue — ils relèvent du curseur de température, qui
 * les rend exactement là où une approximation RVB dérive.
 */
function ColorRow({
  light,
  usable,
  onSend,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  return (
    <div className="flex flex-col items-center gap-3 py-2">
      <ColorWheel
        value={light.colorHex}
        disabled={!usable}
        onChange={(hex) => onSend(light.id, { colorHex: hex, on: true, transitionSec: 0.12 })}
      />
      <span className="text-xs tabular-nums text-neutral-600">
        {light.colorHex ?? 'couleur inconnue'}
      </span>
    </div>
  );
}

/**
 * Température de blanc, en mireds. L'échelle est inversée — une valeur basse est
 * froide — donc le curseur est retourné pour que « vers la droite » veuille dire
 * « plus chaud », comme le dégradé l'annonce.
 */
function TemperatureRow({
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
  const kelvin = Math.round(1_000_000 / shown);

  return (
    <div className="flex flex-col gap-2">
      <span className="text-xs uppercase tracking-wider text-neutral-600">Blanc</span>
      <div className="flex items-center gap-4">
        <input
          type="range"
          min={min}
          max={max}
          value={shown}
          disabled={!usable}
          aria-label="Température de blanc"
          onChange={(event) => {
            const mired = Number(event.currentTarget.value);
            setPending(mired);
            throttled.push(mired);
          }}
          onPointerUp={(event) => throttled.commit(Number(event.currentTarget.value))}
          onKeyUp={(event) => throttled.commit(Number(event.currentTarget.value))}
          className="h-11 flex-1"
          style={{
            accentColor: '#e8e8ea',
            background: 'linear-gradient(to right, #cfe3ff, #ffffff, #ffc27a)',
            borderRadius: '0.5rem',
          }}
        />
        <span className="w-16 text-right text-sm tabular-nums text-neutral-400">{kelvin} K</span>
      </div>
    </div>
  );
}

/** Effets Philips. Aucun intérêt fonctionnel, mais l'ampoule les expose. */
function EffectRow({
  light,
  usable,
  onSend,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  // Les commandes de contrôle du cycle de vie ne sont pas des ambiances : elles
  // n'ont rien à faire dans une grille de choix.
  const control = new Set(['none', 'finish_effect', 'stop_effect', 'stop_hue_effect', 'okay', 'channel_change']);
  const ambiences = light.capabilities.effects.filter((e) => !control.has(e));

  return (
    <div className="flex flex-col gap-2">
      <span className="text-xs uppercase tracking-wider text-neutral-600">Effets</span>
      <div className="flex flex-wrap gap-2">
        {ambiences.map((effect) => (
          <button
            key={effect}
            type="button"
            disabled={!usable}
            onClick={() => onSend(light.id, { effect, on: true })}
            className="min-h-9 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-xs text-neutral-400 active:bg-neutral-800 disabled:opacity-40"
          >
            {EFFECT_LABELS[effect] ?? effect}
          </button>
        ))}
        <button
          type="button"
          disabled={!usable}
          onClick={() => onSend(light.id, { effect: 'stop_effect' })}
          className="min-h-9 rounded-lg border border-neutral-700 px-3 text-xs text-neutral-300 active:bg-neutral-800 disabled:opacity-40"
        >
          Arrêter
        </button>
      </div>
    </div>
  );
}

const EFFECT_LABELS: Record<string, string> = {
  blink: 'Clignoter',
  breathe: 'Respirer',
  candle: 'Bougie',
  fireplace: 'Cheminée',
  colorloop: 'Boucle',
  sunset: 'Coucher',
  sunrise: 'Aurore',
  sparkle: 'Étincelles',
  opal: 'Opale',
  glisten: 'Scintillement',
  underwater: 'Sous-marin',
  cosmos: 'Cosmos',
  sunbeam: 'Rayon',
  enchant: 'Enchanté',
};

/** Ce que fait la lampe quand le courant revient, ou qu'on la rallume au mur. */
function PowerOnRow({
  light,
  usable,
  onSend,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  const labels: Record<string, string> = {
    off: 'Éteinte',
    on: 'Allumée',
    toggle: 'Inverser',
    previous: 'État précédent',
  };

  return (
    <div className="flex flex-col gap-2">
      <span className="text-xs uppercase tracking-wider text-neutral-600">Au rallumage mural</span>
      <div className="flex flex-wrap gap-2">
        {light.capabilities.powerOnBehaviours.map((value) => (
          <button
            key={value}
            type="button"
            disabled={!usable}
            onClick={() => onSend(light.id, { powerOnBehavior: value })}
            className={`min-h-9 rounded-lg border px-3 text-xs transition-colors disabled:opacity-40 ${
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
  );
}

function Diagnostics({
  light,
  usable,
  onError,
}: {
  light: LightSnapshot;
  usable: boolean;
  onError: (message: string) => void;
}) {
  return (
    <div className="flex items-center justify-between gap-3 border-t border-neutral-900 pt-3">
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
        className="min-h-9 rounded-lg border border-neutral-800 px-3 text-xs text-neutral-400 active:bg-neutral-800 disabled:opacity-40"
      >
        Faire clignoter
      </button>
    </div>
  );
}
