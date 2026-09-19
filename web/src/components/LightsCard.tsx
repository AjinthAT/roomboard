import { useEffect, useState } from 'react';
import { UnauthorizedError, setLight } from '../api/client';
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
          <LightRow key={light.id} light={light} live={live} onSend={send} />
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
}: {
  light: LightSnapshot;
  live: boolean;
  onSend: (id: string, command: LightCommand) => void;
}) {
  const usable = live && light.reachable;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <button
          type="button"
          disabled={!usable}
          onClick={() => onSend(light.id, { on: !light.on })}
          className={`size-11 shrink-0 rounded-lg border transition-colors disabled:opacity-40 ${
            light.on ? 'border-neutral-500 bg-neutral-800' : 'border-neutral-800 bg-neutral-900'
          }`}
          aria-label={light.on ? `Éteindre ${light.name}` : `Allumer ${light.name}`}
        >
          <span
            className="mx-auto block size-3 rounded-full"
            style={{
              background: light.on ? (light.colorHex ?? '#e8e8ea') : '#2a2a2e',
            }}
          />
        </button>

        <span className="flex-1 truncate text-neutral-300">{light.name}</span>

        {/* Trois états distincts, et la nuance compte : pas encore appairée n'est
            pas une panne, injoignable en est une, et éteinte n'est ni l'une ni l'autre. */}
        {!light.paired ? (
          <span className="text-xs text-neutral-600">pas encore appairée</span>
        ) : !light.reachable ? (
          <span className="text-xs text-amber-600/80">injoignable</span>
        ) : null}
      </div>

      {light.supportsBrightness && (
        <BrightnessRow light={light} usable={usable} onSend={onSend} />
      )}

      {light.supportsColor && (
        <ColorRow light={light} usable={usable} onSend={onSend} />
      )}
    </div>
  );
}

/**
 * Teintes prédéfinies plutôt qu'un sélecteur de couleur.
 *
 * Un `input type="color"` ouvre une boîte de dialogue système : petites cibles,
 * deux gestes, et un comportement variable selon la version de Safari. Sur un
 * panneau mural on veut un appui, sur une cible d'au moins 44 px
 * (docs/07-frontend.md). Huit teintes couvrent l'usage réel d'une chambre —
 * le reste relèverait d'un éditeur, hors périmètre V1.
 */
const PRESETS: ReadonlyArray<{ hex: string; label: string }> = [
  { hex: '#FFD4A3', label: 'Blanc chaud' },
  { hex: '#FFF1E0', label: 'Blanc neutre' },
  { hex: '#F2F6FF', label: 'Blanc froid' },
  { hex: '#FF4400', label: 'Orange' },
  { hex: '#FF0000', label: 'Rouge' },
  { hex: '#C04CFF', label: 'Violet' },
  { hex: '#2F6BFF', label: 'Bleu' },
  { hex: '#27C46B', label: 'Vert' },
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
      {PRESETS.map((preset) => {
        // La lampe renvoie une couleur convertie depuis ses coordonnées CIE :
        // elle ne retombe presque jamais exactement sur la valeur commandée.
        const active = current !== null && isClose(current, preset.hex);

        return (
          <button
            key={preset.hex}
            type="button"
            title={preset.label}
            aria-label={preset.label}
            disabled={!usable}
            onClick={() => onSend(light.id, { colorHex: preset.hex, on: true })}
            className={`size-11 rounded-lg border-2 transition-colors disabled:opacity-40 ${
              active ? 'border-neutral-200' : 'border-neutral-800'
            }`}
            style={{ background: preset.hex }}
          />
        );
      })}
    </div>
  );
}

/**
 * Proximité RVB grossière : il s'agit de surligner une pastille, pas de mesurer.
 *
 * Le seuil est large à dessein. Une ampoule ne reproduit pas la couleur demandée :
 * elle la ramène dans son gamut physique, et RoomOS réaffiche ce qu'elle émet
 * vraiment plutôt que ce qu'on lui a demandé. Mesuré sur une Philips Hue, l'écart
 * atteint 116 sur cette échelle pour un vert. Un seuil serré ne surlignerait
 * jamais la teinte qu'on vient pourtant de choisir.
 */
function isClose(a: string, b: string): boolean {
  const parse = (hex: string) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
  const [r1, g1, b1] = parse(a);
  const [r2, g2, b2] = parse(b);

  return Math.abs(r1 - r2) + Math.abs(g1 - g2) + Math.abs(b1 - b2) < 150;
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
        step={1}
        value={shown}
        disabled={!usable}
        aria-label={`Luminosité ${light.name}`}
        onChange={(event) => setPending(Number(event.currentTarget.value))}
        onPointerUp={(event) => {
          const level = Number(event.currentTarget.value);
          setPending(level);
          onSend(light.id, { brightness: level, on: level > 0 });
        }}
        className="h-11 flex-1 accent-neutral-300 disabled:opacity-40"
      />
      <span className="w-12 text-right text-sm tabular-nums text-neutral-400">{shown} %</span>
    </div>
  );
}
