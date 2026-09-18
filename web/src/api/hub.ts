import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { roomStore } from '../store/roomStore';
import { readToken } from './token';
import type { PcStateChanged, TelemetryUpdated } from './types';

/**
 * Le serveur pousse, le client n'appelle rien : pour agir, il passe par REST
 * (docs/05-api.md).
 *
 * `accessTokenFactory` est la seule façon d'authentifier un WebSocket depuis un
 * navigateur : l'en-tête part sur la négociation, puis le jeton bascule en query
 * string sur le socket lui-même.
 */
export function connectRoomHub(): HubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl('/hub/room', {
      accessTokenFactory: () => readToken() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on('PcStateChanged', (payload: PcStateChanged) => {
    roomStore.setPcState(payload.id, payload.online, payload.uptimeSec);
  });

  connection.on('TelemetryUpdated', (payload: TelemetryUpdated) => {
    roomStore.setTelemetry(payload.id, payload.telemetry);
  });

  connection.onreconnecting(() => roomStore.setConnection('reconnecting'));
  connection.onreconnected(() => roomStore.setConnection('connected'));
  connection.onclose(() => roomStore.setConnection('offline'));

  connection
    .start()
    .then(() => roomStore.setConnection('connected'))
    .catch(() => roomStore.setConnection('offline'));

  return connection;
}

export function isLive(connection: HubConnection | null): boolean {
  return connection?.state === HubConnectionState.Connected;
}
