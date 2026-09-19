import { useEffect, useRef } from 'react';

/**
 * Envoi limité en cadence, pour les gestes continus.
 *
 * Un curseur ou une roue produisent des dizaines d'événements par seconde. Les
 * envoyer tous saturerait le réseau Zigbee — une ampoule encaisse quelques commandes
 * par seconde, pas soixante. Mais n'envoyer qu'au relâchement donne une interface
 * morte sous le doigt.
 *
 * Compromis : on émet au plus une commande par intervalle pendant le geste, et on
 * garantit que la **dernière** valeur part toujours, même si elle tombe pendant une
 * période de silence. Sans cette garantie finale, on relâcherait parfois sur une
 * valeur qui n'a jamais été envoyée.
 */
export function useThrottledSend<T>(send: (value: T) => void, intervalMs = 160) {
  const lastSentAt = useRef(0);
  const pending = useRef<T | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const sendRef = useRef(send);

  sendRef.current = send;

  useEffect(() => () => {
    if (timer.current) {
      clearTimeout(timer.current);
    }
  }, []);

  function flush() {
    timer.current = null;

    if (pending.current === null) {
      return;
    }

    lastSentAt.current = Date.now();
    const value = pending.current;
    pending.current = null;
    sendRef.current(value);
  }

  return {
    /** Pendant le geste : au plus une commande par intervalle. */
    push(value: T) {
      pending.current = value;
      const elapsed = Date.now() - lastSentAt.current;

      if (elapsed >= intervalMs) {
        flush();
      } else if (!timer.current) {
        timer.current = setTimeout(flush, intervalMs - elapsed);
      }
    },

    /** Au relâchement : la valeur finale part sans attendre. */
    commit(value: T) {
      pending.current = value;

      if (timer.current) {
        clearTimeout(timer.current);
      }

      flush();
    },
  };
}
