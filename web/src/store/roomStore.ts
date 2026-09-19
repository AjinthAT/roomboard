import { useSyncExternalStore } from 'react';
import type {
  AudioSnapshot, LightSnapshot, LightStateChanged, MusicState, PcSnapshot,
  RoutineInfo, SceneInfo, StateSnapshot, Telemetry,
} from '../api/types';
import { MusicLink } from '../api/types';

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'offline';

export type RoomState = {
  roomName: string;
  pc: PcSnapshot | null;
  audio: AudioSnapshot | null;
  lights: LightSnapshot[];
  music: MusicState;
  scenes: SceneInfo[];
  routines: RoutineInfo[];
  /** Exécution en cours ou dernière terminée, pour le retour de progression. */
  sceneRun: SceneRunView | null;
  connection: ConnectionStatus;
};

export type SceneRunView = {
  runId: string;
  sceneId: string;
  done: number;
  failed: number;
  status: 'running' | 'completed' | 'failed' | 'cancelled';
};

const NO_MUSIC: MusicState = {
  link: MusicLink.NotLinked,
  nowPlaying: {
    title: null, artist: null, albumArtUrl: null,
    isPlaying: false, progressMs: null, durationMs: null, deviceName: null,
    shuffle: false, repeat: 'off',
  },
};

const NO_LIGHTS: LightSnapshot[] = [];

const NO_SCENES: SceneInfo[] = [];

const NO_ROUTINES: RoutineInfo[] = [];

const EMPTY: RoomState = {
  roomName: '', pc: null, audio: null, lights: NO_LIGHTS,
  music: NO_MUSIC, scenes: NO_SCENES, routines: NO_ROUTINES,
  sceneRun: null, connection: 'connecting',
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
  private musicReceivedAt = 0;
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
      scenes: snapshot.scenes,
      routines: snapshot.routines,
    });
  }

  setCatalog(scenes: SceneInfo[], routines: RoutineInfo[]): void {
    this.set({ scenes, routines });
  }

  patchRoutine(id: string, patch: Partial<RoutineInfo>): void {
    const index = this.state.routines.findIndex((r) => r.id === id);

    if (index === -1) {
      return;
    }

    const routines = [...this.state.routines];
    routines[index] = { ...routines[index], ...patch };
    this.set({ routines });
  }

  sceneStarted(runId: string, sceneId: string): void {
    this.set({ sceneRun: { runId, sceneId, done: 0, failed: 0, status: 'running' } });
  }

  sceneStepCompleted(runId: string, status: string): void {
    const run = this.state.sceneRun;

    if (!run || run.runId !== runId) {
      return;
    }

    this.set({
      sceneRun: {
        ...run,
        done: run.done + 1,
        failed: run.failed + (status === 'Failed' ? 1 : 0),
      },
    });
  }

  sceneFinished(runId: string, status: string): void {
    const run = this.state.sceneRun;

    if (!run || run.runId !== runId) {
      return;
    }

    this.set({
      sceneRun: { ...run, status: status.toLowerCase() as SceneRunView['status'] },
    });
  }

  setLight(id: string, patch: Partial<Omit<LightStateChanged, 'id'>>): void {
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
    // Horodatage de réception : la barre de progression avance ensuite toute seule
    // côté client, et se recale à chaque message du serveur.
    this.musicReceivedAt = Date.now();
    this.set({ music });
  }

  /** Millisecondes écoulées depuis la dernière position reçue. */
  elapsedSinceMusicUpdate(): number {
    return Date.now() - this.musicReceivedAt;
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
