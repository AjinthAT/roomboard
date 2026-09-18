import { useSyncExternalStore } from 'react';
import type {
  AudioSnapshot, LightSnapshot, LightStateChanged, MusicState, PcSnapshot,
  StateSnapshot, Telemetry,
} from '../api/types';
import { MusicLink } from '../api/types';

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'offline';

export type RoomState = {
  roomName: string;
  pc: PcSnapshot | null;
  audio: AudioSnapshot | null;
  lights: LightSnapshot[];
  music: MusicState;
  connection: ConnectionStatus;
};

const NO_MUSIC: MusicState = {
  link: MusicLink.NotLinked,
  nowPlaying: {
    title: null, artist: null, albumArtUrl: null,
    isPlaying: false, progressMs: null, durationMs: null, deviceName: null,
  },
};

const NO_LIGHTS: LightSnapshot[] = [];

const EMPTY: RoomState = {
  roomName: '', pc: null, audio: null, lights: NO_LIGHTS,
  music: NO_MUSIC, connection: 'connecting',
};

/**
 * Store externe, hors du state React global.
 *
 * Un tick de télémétrie ne doit re-rendre que les lignes dont la valeur a changé :
 * les composants s'abonnent avec un sélecteur qui renvoie une primitive, et
 * `useSyncExternalStore` compare par `Object.is`. Une ligne GPU inchangée ne
 * re-render pas quand le CPU bouge (docs/07-frontend.md).
 */
class RoomStore {
  private listeners = new Set<() => void>();
  private state: RoomState = EMPTY;

  /** Dernière télémétrie reçue, pas encore appliquée (voir le throttle). */
  private pendingTelemetry: Telemetry | null = null;
  private lastApplied = 0;
  private timer: ReturnType<typeof setTimeout> | null = null;

  /** Le serveur plafonne déjà à 1 Hz ; le client ne lui fait pas confiance. */
  private static readonly MinIntervalMs = 1000;

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };

  getSnapshot = (): RoomState => this.state;

  private emit(): void {
    for (const listener of this.listeners) {
      listener();
    }
  }

  private set(next: Partial<RoomState>): void {
    this.state = { ...this.state, ...next };
    this.emit();
  }

  loadSnapshot(snapshot: StateSnapshot): void {
    const pc = snapshot.pcs[0] ?? null;

    this.set({
      roomName: snapshot.room.name,
      pc,
      audio: pc ? (snapshot.audio[pc.id] ?? null) : null,
      lights: snapshot.lights,
      music: snapshot.music,
    });
  }

  setLight(id: string, patch: Omit<LightStateChanged, 'id'>): void {
    const index = this.state.lights.findIndex((l) => l.id === id);

    if (index === -1) {
      return;
    }

    // Nouveau tableau, mais objets inchangés hors de l'indice touché : les lignes
    // des autres lampes ne re-rendent pas.
    const lights = [...this.state.lights];
    lights[index] = { ...lights[index], ...patch };

    this.set({ lights });
  }

  setMusic(music: MusicState): void {
    this.set({ music });
  }

  setAudio(pcId: string, audio: AudioSnapshot): void {
    if (!this.state.pc || this.state.pc.id !== pcId) {
      return;
    }

    this.set({ audio });
  }

  setConnection(connection: ConnectionStatus): void {
    this.set({ connection });
  }

  setPcState(id: string, online: boolean, uptimeSec: number | null): void {
    if (!this.state.pc || this.state.pc.id !== id) {
      return;
    }

    // Un PC éteint n'a pas de télémétrie : la garder afficherait des valeurs mortes.
    this.set({
      pc: {
        ...this.state.pc,
        online,
        uptimeSec,
        telemetry: online ? this.state.pc.telemetry : null,
      },
    });
  }

  /** Throttle à 1 Hz, dernière valeur gagnante. */
  setTelemetry(id: string, telemetry: Telemetry): void {
    if (!this.state.pc || this.state.pc.id !== id) {
      return;
    }

    this.pendingTelemetry = telemetry;

    const elapsed = Date.now() - this.lastApplied;

    if (elapsed >= RoomStore.MinIntervalMs) {
      this.applyTelemetry();
      return;
    }

    this.timer ??= setTimeout(() => {
      this.timer = null;
      this.applyTelemetry();
    }, RoomStore.MinIntervalMs - elapsed);
  }

  private applyTelemetry(): void {
    if (!this.state.pc || !this.pendingTelemetry) {
      return;
    }

    this.lastApplied = Date.now();
    this.set({ pc: { ...this.state.pc, telemetry: this.pendingTelemetry } });
    this.pendingTelemetry = null;
  }

  reset(): void {
    this.state = EMPTY;
    this.pendingTelemetry = null;
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    this.emit();
  }
}

export const roomStore = new RoomStore();

export function useRoom<T>(selector: (state: RoomState) => T): T {
  return useSyncExternalStore(
    roomStore.subscribe,
    () => selector(roomStore.getSnapshot()),
    () => selector(EMPTY),
  );
}
