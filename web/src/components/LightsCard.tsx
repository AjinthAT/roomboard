import { useEffect, useState } from 'react';
import { UnauthorizedError, setLight } from '../api/client';
import type { LightCommand } from '../api/client';
import type { LightSnapshot } from '../api/types';
import { useRoom } from '../store/roomStore';
import { LightSheet } from './LightSheet';
import { useThrottledSend } from './useThrottledSend';

export function LightsCard({ onUnauthorized }: { onUnauthorized: () => void }) {
  const lights = useRoom((s) => s.lights);
  const live = useRoom((s) => s.connection === 'connected');
  const [error, setError] = useState<string | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);

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

  const open = lights.find((l) => l.id === openId) ?? null;

  return (
    <section className="flex flex-col gap-4 rounded-2xl border border-neutral-800 bg-neutral-950 p-4 sm:p-6">
      <h2 className="text-xs font-medium uppercase tracking-[0.2em] text-neutral-500">Lumières</h2>

      {lights.length === 0 ? (
        <p className="text-sm text-neutral-600">Aucune lampe déclarée.</p>
      ) : (
        lights.map((light) => (
          <LightRow
            key={light.id}
            light={light}
            live={live}
            onSend={send}
            onOpen={() => setOpenId(light.id)}
          />
        ))
      )}

      {error && <p className="text-xs text-red-400">{error}</p>}

      {open && (
        <LightSheet
          light={open}
          usable={live && open.reachable}
          onSend={send}
          onError={setError}
          onClose={() => setOpenId(null)}
        />
      )}
    </section>
  );
}

/**
 * Une ligne par lampe : allumer, nommer, doser.
 *
 * Tout le reste — roue, blancs, effets, réglages — vit dans la vue détaillée.
 * L'écran d'accueil doit se lire en moins de deux secondes (docs/07-frontend.md),
 * et une carte qui empile vingt effets ne le permet pas.
 */
function LightRow({
  light,
  live,
  onSend,
  onOpen,
}: {
  light: LightSnapshot;
  live: boolean;
  onSend: (id: string, command: LightCommand) => void;
  onOpen: () => void;
}) {
  const usable = live && light.reachable;

  return (
    <div className="flex flex-col gap-2">
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

        <button
          type="button"
          onClick={onOpen}
          className="flex min-h-11 flex-1 items-center justify-between gap-2 rounded-lg px-1 text-left active:bg-neutral-900"
        >
          <span className="truncate text-neutral-300">{light.name}</span>
          <span className="flex items-center gap-2">
            {!light.paired ? (
              <span className="text-xs text-neutral-600">pas encore appairée</span>
            ) : !light.reachable ? (
              <span className="text-xs text-amber-600/80">injoignable</span>
            ) : null}
            <span aria-hidden className="text-neutral-700">›</span>
          </span>
        </button>
      </div>

      {light.capabilities.brightness && (
        <BrightnessRow light={light} usable={usable} onSend={onSend} />
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
  // l'intervalle d'envoi : sinon les commandes se chevauchent et la lampe traîne.
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
