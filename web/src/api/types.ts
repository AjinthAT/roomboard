/**
 * Types du contrat d'API. Tenus synchrones à la main avec RoomOS.Domain.
 * Voir docs/11-conventions.md.
 */

export type HealthResponse = {
  status: string;
  version: string;
  serverTime: string;
};
