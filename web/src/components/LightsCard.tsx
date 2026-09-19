import { useEffect, useState } from 'react';
import { UnauthorizedError, identifyLight, setLight } from '../api/client';
import type { LightCommand } from '../api/client';
import type { LightSnapshot } from '../api/types';
import { useRoom } from '../store/roomStore';

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
  // Même compromis que le volume audio : le curseur suit le doigt, puis repasse
  // sous contrôle du serveur dès qu'il le rejoint.
  const [pending, setPending] = useState<number | null>(null);
  const serverValue = light.brightness ?? 0;

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
        onChange={(event) => setPending(Number(event.currentTarget.value))}
        onPointerUp={(event) => {
          const level = Number(event.currentTarget.value);
          setPending(level);
          onSend(light.id, { brightness: level, on: level > 0, transitionSec: 0.3 });
        }}
        className="h-11 flex-1 accent-neutral-300 disabled:opacity-40"
      />
      <span className="w-12 text-right text-sm tabular-nums text-neutral-400">{shown} %</span>
    </div>
  );
}

/**
 * Teintes prédéfinies plutôt qu'un sélecteur de couleur.
 *
 * Un `input type="color"` ouvre une boîte de dialogue système : petites cibles,
 * deux gestes, comportement variable selon la version de Safari. Sur un panneau
 * mural on veut un appui sur une cible d'au moins 44 px (docs/07-frontend.md).
 */
const PRESETS: ReadonlyArray<{ hex: string; label: string }> = [
  { hex: '#FF4400', label: 'Orange' },
  { hex: '#FF0000', label: 'Rouge' },
  { hex: '#FF2E88', label: 'Rose' },
  { hex: '#C04CFF', label: 'Violet' },
  { hex: '#2F6BFF', label: 'Bleu' },
  { hex: '#00C8D7', label: 'Cyan' },
  { hex: '#27C46B', label: 'Vert' },
  { hex: '#D7DB2A', label: 'Citron' },
];

function ColorRow({
  light,
  usable,
  onSend,
}: {
  light: LightSnapshot;
  usable: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  const current = light.colorHex?.toUpperCase() ?? null;

  return (
    <div className="flex flex-wrap gap-2 pl-14">
      {PRESETS.map((preset) => (
        <button
          key={preset.hex}
          type="button"
          title={preset.label}
          aria-label={preset.label}
          disabled={!usable}
          onClick={() => onSend(light.id, { colorHex: preset.hex, on: true, transitionSec: 0.5 })}
          className={`size-11 rounded-lg border-2 transition-colors disabled:opacity-40 ${
            current !== null && isClose(current, preset.hex)
              ? 'border-neutral-200'
              : 'border-neutral-800'
          }`}
          style={{ background: preset.hex }}
        />
      ))}
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
          onChange={(event) => setPending(Number(event.currentTarget.value))}
          onPointerUp={(event) => {
            const mired = Number(event.currentTarget.value);
            setPending(mired);
            onSend(light.id, { colorTempMired: mired, on: true, transitionSec: 0.5 });
          }}
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

/**
 * Proximité RVB grossière : il s'agit de surligner une pastille, pas de mesurer.
 *
 * Le seuil est large à dessein. Une ampoule ne reproduit pas la couleur demandée :
 * elle la ramène dans son gamut physique, et RoomOS réaffiche ce qu'elle émet
 * vraiment. Mesuré sur une Philips Hue, l'écart atteint 116 sur cette échelle pour
 * un vert — un seuil serré ne surlignerait jamais la teinte qu'on vient de choisir.
 */
function isClose(a: string, b: string): boolean {
  const parse = (hex: string) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
  const [r1, g1, b1] = parse(a);
  const [r2, g2, b2] = parse(b);

  return Math.abs(r1 - r2) + Math.abs(g1 - g2) + Math.abs(b1 - b2) < 150;
}
