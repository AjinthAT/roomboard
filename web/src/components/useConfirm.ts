import { useEffect, useState } from 'react';

/**
 * Confirmation en deux temps sur le bouton lui-même.
 *
 * Pas de fenêtre modale : sur un panneau mural, une modale demande de viser une
 * petite cible, et la moitié du temps on la ferme par réflexe. Le bouton change de
 * libellé et attend un second appui, puis se désarme tout seul — un bouton laissé
 * armé serait un piège pour le passage suivant.
 */
export function useConfirm(timeoutMs = 4000) {
  const [armed, setArmed] = useState(false);

  useEffect(() => {
    if (!armed) {
      return;
    }

    const timer = setTimeout(() => setArmed(false), timeoutMs);
    return () => clearTimeout(timer);
  }, [armed, timeoutMs]);

  return {
    armed,
    arm: () => setArmed(true),
    disarm: () => setArmed(false),
  };
}
