import { useEffect, useState } from 'react';
import { getQueue, queueTrack, searchTracks } from '../api/client';
import type { MusicTrack } from '../api/types';

/**
 * Recherche et file d'attente, en plein écran.
 *
 * Elles ne tiennent pas dans la carte : une liste de résultats et vingt morceaux en
 * attente noieraient la pochette et les contrôles, qui sont ce qu'on regarde en
 * passant. Ici on cherche délibérément, puis on referme.
 */
export function MusicBrowser({
  onError,
  onClose,
}: {
  onError: (message: string) => void;
  onClose: () => void;
}) {
  const [tab, setTab] = useState<'search' | 'queue'>('search');
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<MusicTrack[]>([]);
  const [queue, setQueue] = useState<MusicTrack[]>([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  // La recherche part après une pause de frappe : Spotify est une API tierce, et
  // une requête par caractère est un bon moyen de se faire limiter.
  useEffect(() => {
    if (tab !== 'search' || query.trim().length < 2) {
      setResults([]);
      return;
    }

    const controller = new AbortController();
    const timer = setTimeout(() => {
      setBusy(true);
      searchTracks(query.trim(), controller.signal)
        .then(setResults)
        .catch(() => undefined)
        .finally(() => setBusy(false));
    }, 350);

    return () => {
      controller.abort();
      clearTimeout(timer);
    };
  }, [query, tab]);

  useEffect(() => {
    if (tab !== 'queue') {
      return;
    }

    const controller = new AbortController();
    getQueue(controller.signal).then(setQueue).catch(() => undefined);
    return () => controller.abort();
  }, [tab]);

  function add(track: MusicTrack) {
    queueTrack(track.uri).catch((cause: unknown) =>
      onError(cause instanceof Error ? cause.message : 'Échec'));
  }

  const shown = tab === 'search' ? results : queue;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Musique"
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/70 sm:items-center sm:p-6"
      onPointerDown={(e) => {
        if (e.target === e.currentTarget) {
          onClose();
        }
      }}
    >
      <div className="flex max-h-full w-full max-w-md flex-col gap-4 overflow-hidden rounded-t-3xl border border-neutral-800 bg-neutral-950 p-6 sm:rounded-3xl">
        <header className="flex items-center justify-between">
          <h2 className="text-lg text-neutral-100">Musique</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Fermer"
            className="min-h-11 min-w-11 rounded-lg border border-neutral-800 text-neutral-400 active:bg-neutral-900"
          >
            ✕
          </button>
        </header>

        <div className="flex gap-2 rounded-xl border border-neutral-900 bg-neutral-900/40 p-1">
          {(['search', 'queue'] as const).map((t) => (
            <button
              key={t}
              type="button"
              onClick={() => setTab(t)}
              className={`min-h-10 flex-1 rounded-lg text-sm transition-colors ${
                tab === t ? 'bg-neutral-800 text-neutral-100' : 'text-neutral-500'
              }`}
            >
              {t === 'search' ? 'Rechercher' : 'À suivre'}
            </button>
          ))}
        </div>

        {tab === 'search' && (
          <input
            type="search"
            value={query}
            autoFocus
            placeholder="Titre, artiste…"
            onChange={(e) => setQuery(e.currentTarget.value)}
            className="min-h-11 rounded-lg border border-neutral-800 bg-neutral-900 px-4 text-neutral-100 outline-none focus:border-neutral-600"
          />
        )}

        <div className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto">
          {shown.length === 0 ? (
            <p className="py-6 text-center text-sm text-neutral-600">
              {tab === 'search'
                ? busy ? 'Recherche…'
                  : query.trim().length < 2 ? 'Tape au moins deux lettres.'
                  : 'Aucun résultat.'
                : 'File vide.'}
            </p>
          ) : (
            shown.map((track, index) => (
              <div key={`${track.uri}-${index}`} className="flex items-center gap-3">
                <div className="size-11 shrink-0 overflow-hidden rounded bg-neutral-900">
                  {track.albumArtUrl && (
                    <img
                      src={track.albumArtUrl}
                      alt=""
                      width={44}
                      height={44}
                      loading="lazy"
                      className="size-11 object-cover"
                    />
                  )}
                </div>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm text-neutral-200">{track.title}</p>
                  <p className="truncate text-xs text-neutral-600">{track.artist}</p>
                </div>
                {tab === 'search' && (
                  <button
                    type="button"
                    onClick={() => add(track)}
                    aria-label={`Ajouter ${track.title} à la file`}
                    className="min-h-11 min-w-11 rounded-lg border border-neutral-800 text-neutral-400 active:bg-neutral-800"
                  >
                    +
                  </button>
                )}
              </div>
            ))
          )}
        </div>

        {tab === 'search' && results.length === 10 && (
          // Le plafond vient de Spotify, pas de RoomOS. Le dire évite de chercher
          // pourquoi le onzième résultat manque.
          <p className="text-xs text-neutral-700">Spotify limite la recherche à dix résultats.</p>
        )}
      </div>
    </div>
  );
}
