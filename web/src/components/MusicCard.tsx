import { useState } from 'react';
import { UnauthorizedError, runMusicAction } from '../api/client';
import type { MusicAction } from '../api/client';
import { MusicLink } from '../api/types';
import { useRoom } from '../store/roomStore';
import { DevicePicker, MusicVolume, Playlists, TrackProgress } from './MusicExtras';

export function MusicCard({ onUnauthorized }: { onUnauthorized: () => void }) {
  const link = useRoom((s) => s.music.link);
  const live = useRoom((s) => s.connection === 'connected');
  const title = useRoom((s) => s.music.nowPlaying.title);
  const isPlaying = useRoom((s) => s.music.nowPlaying.isPlaying);

  const [error, setError] = useState<string | null>(null);

  async function run(action: MusicAction) {
    setError(null);
    try {
      await runMusicAction(action);
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
      <header className="flex items-baseline justify-between">
        <h2 className="text-xs font-medium uppercase tracking-[0.2em] text-neutral-500">Musique</h2>
        {link === MusicLink.NoActiveDevice && (
          <span className="text-sm text-neutral-500">Aucun appareil actif</span>
        )}
        {link === MusicLink.Denied && (
          <span className="text-sm text-amber-400">Accès refusé</span>
        )}
      </header>

      {link === MusicLink.NotLinked ? <NotLinked />
        : link === MusicLink.Denied ? <Denied />
        : <Track />}

      {link === MusicLink.Ready && <TrackProgress />}

      <div className="flex gap-3">
        <Control label="Précédent" glyph="◀◀" disabled={!live || !title} onClick={() => run('previous')} />
        <Control
          label={isPlaying ? 'Pause' : 'Lecture'}
          glyph={isPlaying ? '❚❚' : '▶'}
          disabled={!live}
          onClick={() => run(isPlaying ? 'pause' : 'play')}
        />
        <Control label="Suivant" glyph="▶▶" disabled={!live || !title} onClick={() => run('next')} />
      </div>

      {link !== MusicLink.NotLinked && (
        <>
          <MusicVolume usable={live} onError={setError} />
          <DevicePicker usable={live} onError={setError} onUnauthorized={onUnauthorized} />
          <Playlists usable={live} onError={setError} />
        </>
      )}

      {error && <p className="text-xs text-red-400">{error}</p>}
    </section>
  );
}

/**
 * L'autorisation se fait une fois, depuis un navigateur, à travers un tunnel SSH :
 * Spotify n'accepte plus qu'une adresse de bouclage en HTTP comme redirection
 * (docs/09-integrations.md). L'iPad ne peut donc pas la mener lui-même.
 */
function NotLinked() {
  return (
    <p className="text-sm text-neutral-500">
      Spotify n'est pas encore autorisé. L'autorisation se fait une fois depuis un
      ordinateur, pas depuis cet écran.
    </p>
  );
}

/**
 * Spotify refuse l'accès. La cause la plus fréquente n'est pas une panne mais une
 * configuration : le compte doit figurer dans la liste d'utilisateurs autorisés de
 * l'application, faute de quoi l'API renvoie 403 sur tout (docs/09-integrations.md).
 */
function Denied() {
  return (
    <p className="text-sm text-neutral-500">
      Spotify refuse l'accès. Vérifie que ton compte figure bien dans la liste
      d'utilisateurs autorisés de l'application, ou relance l'autorisation.
    </p>
  );
}

function Track() {
  const title = useRoom((s) => s.music.nowPlaying.title);
  const artist = useRoom((s) => s.music.nowPlaying.artist);
  const cover = useRoom((s) => s.music.nowPlaying.albumArtUrl);
  const device = useRoom((s) => s.music.nowPlaying.deviceName);

  if (!title) {
    return <p className="text-sm text-neutral-600">Rien en lecture.</p>;
  }

  return (
    <div className="flex items-center gap-4">
      {/* Dimension fixe et chargement paresseux : une seule image à la fois sur un
          iPad de 2 Go (docs/07-frontend.md). */}
      <div className="size-16 shrink-0 overflow-hidden rounded-lg bg-neutral-900">
        {cover && (
          <img src={cover} alt="" width={64} height={64} loading="lazy" className="size-16 object-cover" />
        )}
      </div>

      <div className="min-w-0">
        <p className="truncate text-neutral-200">{title}</p>
        {artist && <p className="truncate text-sm text-neutral-500">{artist}</p>}
        {device && <p className="truncate text-xs text-neutral-600">{device}</p>}
      </div>
    </div>
  );
}

function Control({
  label,
  glyph,
  disabled,
  onClick,
}: {
  label: string;
  glyph: string;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      disabled={disabled}
      onClick={onClick}
      className="min-h-11 flex-1 rounded-lg border border-neutral-800 bg-neutral-900 text-neutral-300 transition-colors active:bg-neutral-800 disabled:opacity-40"
    >
      {glyph}
    </button>
  );
}
