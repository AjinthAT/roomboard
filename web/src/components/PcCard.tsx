import { useRoom } from '../store/roomStore';
import { ActionButton } from './ActionButton';

export function PcCard({ onUnauthorized }: { onUnauthorized: () => void }) {
  const id = useRoom((s) => s.pc?.id ?? null);
  const name = useRoom((s) => s.pc?.name ?? '');
  const online = useRoom((s) => s.pc?.online ?? false);

  // Hub coupé : l'état affiché peut être périmé, on ne laisse pas agir dessus
  // (docs/07-frontend.md). Les boutons restent visibles, comme spécifié.
  const live = useRoom((s) => s.connection === 'connected');

  if (!id) {
    return null;
  }

  return (
    <section className="flex flex-col gap-5 rounded-2xl border border-neutral-800 bg-neutral-950 p-6">
      <header className="flex items-baseline justify-between">
        <h2 className="text-xs font-medium uppercase tracking-[0.2em] text-neutral-500">{name}</h2>
        <StatusLine online={online} />
      </header>

      <div className="flex flex-col gap-2">
        <Metric label="CPU" usage={(s) => s.pc?.telemetry?.cpuUsage ?? null} temp={(s) => s.pc?.telemetry?.cpuTempC ?? null} />
        <Metric label="GPU" usage={(s) => s.pc?.telemetry?.gpuUsage ?? null} temp={(s) => s.pc?.telemetry?.gpuTempC ?? null} />
        <MemoryRow />
      </div>

      <div className="flex flex-wrap gap-3">
        <ActionButton pcId={id} action="wake" label="Allumer" disabled={!live || online} onUnauthorized={onUnauthorized} />
        <ActionButton pcId={id} action="restart" label="Redémarrer" disabled={!live || !online} onUnauthorized={onUnauthorized} />
        <ActionButton pcId={id} action="shutdown" label="Éteindre" disabled={!live || !online} onUnauthorized={onUnauthorized} />
      </div>
    </section>
  );
}

function StatusLine({ online }: { online: boolean }) {
  const uptime = useRoom((s) => s.pc?.uptimeSec ?? null);
  const live = useRoom((s) => s.connection === 'connected');

  // Sans hub, on ne sait plus : on le dit au lieu d'afficher un état figé
  // comme s'il était frais.
  if (!live) {
    return (
      <span className="flex items-center gap-2 text-sm text-neutral-500">
        <span className="size-2 rounded-full bg-amber-600" />
        État inconnu
      </span>
    );
  }

  return (
    <span className="flex items-center gap-2 text-sm text-neutral-400">
      <span className={`size-2 rounded-full ${online ? 'bg-emerald-500' : 'bg-neutral-700'}`} />
      {online ? `En ligne${uptime ? ` · ${formatUptime(uptime)}` : ''}` : 'Hors ligne'}
    </span>
  );
}

/**
 * Chaque ligne s'abonne à ses propres valeurs. Un tick CPU ne re-render pas la
 * ligne GPU : le sélecteur renvoie une primitive, comparée par `Object.is`.
 */
function Metric({
  label,
  usage,
  temp,
}: {
  label: string;
  usage: (state: import('../store/roomStore').RoomState) => number | null;
  temp: (state: import('../store/roomStore').RoomState) => number | null;
}) {
  const usageValue = useRoom(usage);
  const tempValue = useRoom(temp);

  return (
    <div className="flex items-center gap-4 tabular-nums">
      <span className="w-10 text-sm text-neutral-500">{label}</span>
      <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-neutral-900">
        <div
          className="h-full rounded-full bg-neutral-400 transition-[width] duration-500"
          style={{ width: `${Math.min(100, Math.max(0, usageValue ?? 0))}%` }}
        />
      </div>
      <span className="w-14 text-right text-sm text-neutral-300">
        {usageValue === null ? '—' : `${Math.round(usageValue)} %`}
      </span>
      <span className="w-14 text-right text-sm text-neutral-500">
        {/* Une température absente est un cas nominal, pas une erreur. */}
        {tempValue === null ? '—' : `${Math.round(tempValue)} °C`}
      </span>
    </div>
  );
}

function MemoryRow() {
  const used = useRoom((s) => s.pc?.telemetry?.ramUsedMb ?? null);
  const total = useRoom((s) => s.pc?.telemetry?.ramTotalMb ?? null);

  const percent = used !== null && total ? (used / total) * 100 : null;

  return (
    <div className="flex items-center gap-4 tabular-nums">
      <span className="w-10 text-sm text-neutral-500">RAM</span>
      <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-neutral-900">
        <div
          className="h-full rounded-full bg-neutral-400 transition-[width] duration-500"
          style={{ width: `${Math.min(100, Math.max(0, percent ?? 0))}%` }}
        />
      </div>
      <span className="w-14 text-right text-sm text-neutral-300">
        {percent === null ? '—' : `${Math.round(percent)} %`}
      </span>
      <span className="w-14 text-right text-sm text-neutral-500">
        {used === null || !total ? '—' : `${(used / 1024).toFixed(1)} Go`}
      </span>
    </div>
  );
}

function formatUptime(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  return hours > 0 ? `${hours} h ${minutes} min` : `${minutes} min`;
}
