import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { HubConnection, IRetryPolicy, RetryContext } from '@microsoft/signalr';
import { roomStore } from '../store/roomStore';
import { getState } from './client';
import { readToken } from './token';
import type {
  AudioStateChanged, CatalogChanged, LightStateChanged, NowPlayingChanged, PcStateChanged,
  SceneFinished, SceneStarted, SceneStepCompleted, TelemetryUpdated,
} from './types';

/**
 * Backoff exponentiel plafonné, **sans limite de tentatives**.
 *
 * La politique par défaut de SignalR abandonne après quatre essais, soit 42 secondes.
 * RoomOS est un panneau fixe allumé en permanence : une coupure de plus de 42 s —
 * redémarrage du Core, iPad mis en veille, Wi-Fi qui tombe — laissait l'écran sur
 * « Connexion perdue » définitivement, jusqu'à ce qu'on recharge la page à la main.
 */
class PanelRetryPolicy implements IRetryPolicy {
  private static readonly CapMs = 15_000;

  nextRetryDelayInMilliseconds(context: RetryContext): number {
    return Math.min(2 ** context.previousRetryCount * 1000, PanelRetryPolicy.CapMs);
  }
}

/**
 * Le serveur pousse, le client n'appelle rien : pour agir, il passe par REST
 * (docs/05-api.md).
 *
 * `accessTokenFactory` est la seule façon d'authentifier un WebSocket depuis un
 * navigateur : l'en-tête part sur la négociation, puis le jeton bascule en query
 * string sur le socket lui-même.
 */
export function connectRoomHub(): { stop: () => void } {
  let stopped = false;

  const connection: HubConnection = new HubConnectionBuilder()
    .withUrl('/hub/room', { accessTokenFactory: () => readToken() ?? '' })
    .withAutomaticReconnect(new PanelRetryPolicy())
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on('PcStateChanged', (payload: PcStateChanged) => {
    roomStore.setPcState(payload.id, payload.online, payload.uptimeSec);
  });

  connection.on('TelemetryUpdated', (payload: TelemetryUpdated) => {
    roomStore.setTelemetry(payload.id, payload.telemetry);
  });

  connection.on('AudioStateChanged', (payload: AudioStateChanged) => {
    roomStore.setAudio(payload.pcId, {
      activeOutputId: payload.activeOutputId,
      volume: payload.volume,
      muted: payload.muted,
      outputs: payload.outputs,
    });
  });

  connection.on('NowPlayingChanged', (payload: NowPlayingChanged) => {
    roomStore.setMusic({ link: payload.link, nowPlaying: payload.nowPlaying });
  });

  connection.on('LightStateChanged', (payload: LightStateChanged) => {
    const { id, ...rest } = payload;
    roomStore.setLight(id, rest);
  });

  connection.on('CatalogChanged', (p: CatalogChanged) =>
    roomStore.setCatalog(p.scenes, p.routines));

  connection.on('SceneStarted', (p: SceneStarted) => roomStore.sceneStarted(p.runId, p.sceneId));
  connection.on('SceneStepCompleted', (p: SceneStepCompleted) =>
    roomStore.sceneStepCompleted(p.runId, p.status));
  connection.on('SceneFinished', (p: SceneFinished) => roomStore.sceneFinished(p.runId, p.status));

  connection.onreconnecting(() => roomStore.setConnection('reconnecting'));

  connection.onreconnected(() => {
    // Les deltas émis pendant la coupure sont perdus : sans resynchronisation,
    // l'écran repasse au vert en affichant un état arbitrairement périmé.
    void resync();
  });

  connection.onclose(() => {
    if (stopped) {
      return;
    }
    // La politique ci-dessus ne renonce jamais ; si on arrive ici, c'est que la
    // connexion n'avait jamais abouti. On relance le cycle complet.
    roomStore.setConnection('offline');
    void start();
  });

  async function resync(): Promise<void> {
    try {
      roomStore.loadSnapshot(await getState());
      roomStore.setConnection('connected');
    } catch {
      roomStore.setConnection('offline');
    }
  }

  async function start(attempt = 0): Promise<void> {
    if (stopped) {
      return;
    }

    try {
      await connection.start();
      await resync();
    } catch {
      if (stopped) {
        return;
      }
      roomStore.setConnection('offline');
      const delay = Math.min(2 ** attempt * 1000, 15_000);
      setTimeout(() => void start(attempt + 1), delay);
    }
  }

  void start();

  return {
    stop: () => {
      stopped = true;
      void connection.stop();
    },
  };
}
