import { useEffect, useState } from 'react';
import {
  UnauthorizedError, getMusicDevices, getPlaylists, playUri, seekMusic, setMusicVolume,
  setRepeat, setShuffle, transferMusic,
} from '../api/client';
import type { MusicDevice, Playlist } from '../api/types';
import { roomStore, useRoom } from '../store/roomStore';

/**
 * Barre de progression du morceau.
 *
 * Le serveur ne pousse la position qu'au changement de morceau et toutes les 15 s :
 * sonder Spotify toutes les 3 s et tout rediffuser ferait vingt messages par minute
 * pour une valeur qui avance de façon parfaitement prévisible. La barre progresse
 * donc localement et se recale à chaque message reçu.
 */
export function TrackProgress() {
  const durationMs = useRoom((s) => s.music.nowPlaying.durationMs);
  const progressMs = useRoom((s) => s.music.nowPlaying.progressMs);
  const isPlaying = useRoom((s) => s.music.nowPlaying.isPlaying);

  const [, tick] = useState(0);

  useEffect(() => {
    if (!isPlaying) {
      return;
    }

    const timer = setInterval(() => tick((n) => n + 1), 1000);
    return () => clearInterval(timer);
  }, [isPlaying]);

  if (progressMs === null || !durationMs) {
    return null;
  }

  const elapsed = isPlaying ? roomStore.elapsedSinceMusicUpdate() : 0;
  const position = Math.min(progressMs + elapsed, durationMs);
  const percent = (position / durationMs) * 100;

  return (
    <div className="flex items-center gap-3 text-xs tabular-nums text-neutral-600">
      <span>{formatTime(position)}</span>
      {/* Cliquable : on se déplace dans le morceau en touchant la barre. La zone
          tactile fait 44 px de haut, la barre visible n'en fait qu'une poignée. */}
      <button
        type="button"
        aria-label="Se déplacer dans le morceau"
        className="flex h-11 flex-1 items-center"
        onPointerDown={(e) => {
          const box = e.currentTarget.getBoundingClientRect();
          const ratio = Math.min(1, Math.max(0, (e.clientX - box.left) / box.width));
          void seekMusic(Math.round(ratio * durationMs)).catch(() => undefined);
        }}
      >
        <span className="block h-1 w-full overflow-hidden rounded-full bg-neutral-900">
          <span className="block h-full rounded-full bg-neutral-500" style={{ width: `${percent}%` }} />
        </span>
      </button>
      <span>{formatTime(durationMs)}</span>
    </div>
  );
}

/** Lecture aléatoire et répétition, à côté des contrôles de transport. */
export function PlayModes({ usable, onError }: { usable: boolean; onError: (m: string) => void }) {
  const shuffle = useRoom((s) => s.music.nowPlaying.shuffle);
  const repeat = useRoom((s) => s.music.nowPlaying.repeat);

  // off -> tout le contexte -> le morceau seul -> off. Le même bouton fait le tour,
  // comme sur n'importe quel lecteur.
  const nextRepeat = repeat === 'off' ? 'context' : repeat === 'context' ? 'track' : 'off';
  const repeatLabel = repeat === 'track' ? '🔂' : '🔁';

  function run(action: () => Promise<unknown>) {
    action().catch((cause: unknown) =>
      onError(cause instanceof Error ? cause.message : 'Échec'));
  }

  return (
    <div className="flex gap-3">
      <button
        type="button"
        disabled={!usable}
        aria-pressed={shuffle}
        aria-label="Lecture aléatoire"
        onClick={() => run(() => setShuffle(!shuffle))}
        className={`min-h-11 flex-1 rounded-lg border text-sm transition-colors disabled:opacity-40 ${
          shuffle
            ? 'border-neutral-500 bg-neutral-800 text-neutral-100'
            : 'border-neutral-800 bg-neutral-900 text-neutral-500'
        }`}
      >
        🔀
      </button>
      <button
        type="button"
        disabled={!usable}
        aria-label="Répétition"
        onClick={() => run(() => setRepeat(nextRepeat))}
        className={`min-h-11 flex-1 rounded-lg border text-sm transition-colors disabled:opacity-40 ${
          repeat !== 'off'
            ? 'border-neutral-500 bg-neutral-800 text-neutral-100'
            : 'border-neutral-800 bg-neutral-900 text-neutral-500'
        }`}
      >
        {repeatLabel}
      </button>
    </div>
  );
}

function formatTime(ms: number): string {
  const total = Math.floor(ms / 1000);
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
}

/** Volume Spotify, distinct du volume Windows piloté par la carte Audio. */
export function MusicVolume({ usable, onError }: { usable: boolean; onError: (m: string) => void }) {
  const [level, setLevel] = useState(50);

  async function commit(value: number) {
    setLevel(value);
    try {
      await setMusicVolume(value);
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'Échec');
    }
  }

  return (
    <div className="flex items-center gap-3">
      <span className="w-16 shrink-0 text-xs uppercase tracking-wider text-neutral-600">Spotify</span>
      <input
        type="range"
        min={0}
        max={100}
        value={level}
        disabled={!usable}
        aria-label="Volume Spotify"
        onChange={(e) => setLevel(Number(e.currentTarget.value))}
        onPointerUp={(e) => void commit(Number(e.currentTarget.value))}
        onKeyUp={(e) => void commit(Number(e.currentTarget.value))}
        className="h-11 flex-1 accent-neutral-300 disabled:opacity-40"
      />
      <span className="w-10 text-right text-sm tabular-nums text-neutral-400">{level} %</span>
    </div>
  );
}

/** Choix de l'appareil de lecture : le PC, le téléphone, une enceinte. */
export function DevicePicker({
  usable,
  onError,
  onUnauthorized,
}: {
  usable: boolean;
  onError: (m: string) => void;
  onUnauthorized: () => void;
}) {
  const [devices, setDevices] = useState<MusicDevice[]>([]);
  const deviceName = useRoom((s) => s.music.nowPlaying.deviceName);

  // Rechargé quand l'appareil actif change : inutile d'interroger Spotify en continu
  // pour une liste qui ne bouge qu'au branchement d'un appareil.
  useEffect(() => {
    const controller = new AbortController();

    getMusicDevices(controller.signal)
      .then(setDevices)
      .catch((cause: unknown) => {
        if (cause instanceof UnauthorizedError) {
          onUnauthorized();
        }
      });

    return () => controller.abort();
  }, [deviceName, onUnauthorized]);

  if (devices.length <= 1) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-2">
      {devices.map((device) => (
        <button
          key={device.id}
          type="button"
          disabled={!usable || device.isActive}
          onClick={() => {
            transferMusic(device.id).catch((cause: unknown) =>
              onError(cause instanceof Error ? cause.message : 'Échec'));
          }}
          className={`min-h-9 rounded-lg border px-3 text-xs transition-colors disabled:opacity-40 ${
            device.isActive
              ? 'border-neutral-600 bg-neutral-800 text-neutral-200'
              : 'border-neutral-800 bg-neutral-900 text-neutral-400 active:bg-neutral-800'
          }`}
        >
          {device.name}
        </button>
      ))}
    </div>
  );
}

/** Playlists déclarées en configuration : il n'y a pas d'éditeur en V1. */
export function Playlists({
  usable,
  onError,
}: {
  usable: boolean;
  onError: (m: string) => void;
}) {
  const [playlists, setPlaylists] = useState<Playlist[]>([]);

  useEffect(() => {
    const controller = new AbortController();
    getPlaylists(controller.signal).then(setPlaylists).catch(() => undefined);
    return () => controller.abort();
  }, []);

  if (playlists.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-2">
      {playlists.map((playlist) => (
        <button
          key={playlist.uri}
          type="button"
          disabled={!usable}
          onClick={() => {
            playUri(playlist.uri).catch((cause: unknown) =>
              onError(cause instanceof Error ? cause.message : 'Échec'));
          }}
          className="min-h-9 rounded-lg border border-neutral-800 bg-neutral-900 px-3 text-xs text-neutral-400 transition-colors active:bg-neutral-800 disabled:opacity-40"
        >
          {playlist.name}
        </button>
      ))}
    </div>
  );
}
