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

        {/* Injoignable n'est pas éteinte : la lampe est coupée au mur ou hors de
            portée, et on ne peut rien lui demander. */}
        {!light.reachable && <span className="text-xs text-neutral-600">injoignable</span>}
      </div>

      {light.supportsBrightness && (
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
