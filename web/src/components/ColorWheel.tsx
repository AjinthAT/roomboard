import { useRef, useState } from 'react';
import { useThrottledSend } from './useThrottledSend';

/**
 * Roue de teinte et saturation.
 *
 * Le fond est fait de deux dégradés CSS superposés : un `conic-gradient` pour la
 * teinte, un `radial-gradient` blanc pour la saturation qui décroît vers le centre.
 * Aucune dépendance, aucun canvas, et le rendu est net à toute résolution — ce qui
 * compte sur un iPad de 2017 (docs/07-frontend.md).
 *
 * La luminosité n'est pas sur la roue : elle a son propre curseur, et la mélanger
 * ici donnerait un disque sombre illisible aux faibles valeurs.
 */
export function ColorWheel({
  value,
  disabled,
  onChange,
}: {
  value: string | null;
  disabled: boolean;
  onChange: (hex: string) => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [dragging, setDragging] = useState<{ h: number; s: number } | null>(null);
  const throttled = useThrottledSend<string>(onChange);

  const shown = dragging ?? (value ? hexToHs(value) : null);

  function positionFrom(event: React.PointerEvent): { h: number; s: number } | null {
    const box = ref.current?.getBoundingClientRect();

    if (!box) {
      return null;
    }

    const radius = box.width / 2;
    const dx = event.clientX - (box.left + radius);
    const dy = event.clientY - (box.top + radius);

    // L'angle part du haut et tourne dans le sens horaire, comme le conic-gradient.
    const angle = (Math.atan2(dy, dx) * 180) / Math.PI + 90;

    return {
      h: (angle + 360) % 360,
      s: Math.min(1, Math.hypot(dx, dy) / radius),
    };
  }

  function handle(event: React.PointerEvent, final: boolean) {
    if (disabled) {
      return;
    }

    const point = positionFrom(event);

    if (!point) {
      return;
    }

    setDragging(point);
    const hex = hsToHex(point.h, point.s);

    if (final) {
      throttled.commit(hex);
      setDragging(null);
    } else {
      throttled.push(hex);
    }
  }

  const handleStyle = shown
    ? {
        left: `${50 + Math.sin((shown.h * Math.PI) / 180) * shown.s * 50}%`,
        top: `${50 - Math.cos((shown.h * Math.PI) / 180) * shown.s * 50}%`,
      }
    : { left: '50%', top: '50%' };

  return (
    <div
      ref={ref}
      role="slider"
      aria-label="Couleur"
      aria-valuetext={value ?? 'aucune'}
      aria-valuenow={Math.round(shown?.h ?? 0)}
      aria-valuemin={0}
      aria-valuemax={360}
      tabIndex={disabled ? -1 : 0}
      onPointerDown={(e) => {
        e.currentTarget.setPointerCapture(e.pointerId);
        handle(e, false);
      }}
      onPointerMove={(e) => {
        if (e.buttons > 0) {
          handle(e, false);
        }
      }}
      onPointerUp={(e) => handle(e, true)}
      className={`relative mx-auto size-48 touch-none rounded-full ${
        disabled ? 'opacity-40' : 'cursor-pointer'
      }`}
      style={{
        background:
          'radial-gradient(circle closest-side, #ffffff 0%, rgba(255,255,255,0) 70%), ' +
          'conic-gradient(from 0deg, #ff0000, #ffff00, #00ff00, #00ffff, #0000ff, #ff00ff, #ff0000)',
      }}
    >
      <span
        aria-hidden
        className="pointer-events-none absolute size-6 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white shadow"
        style={{ ...handleStyle, background: value ?? '#ffffff' }}
      />
    </div>
  );
}

/** Teinte et saturation vers hexadécimal, à luminosité maximale. */
function hsToHex(h: number, s: number): string {
  const f = (n: number) => {
    const k = (n + h / 60) % 6;
    const v = 1 - s * Math.max(0, Math.min(k, 4 - k, 1));
    return Math.round(v * 255)
      .toString(16)
      .padStart(2, '0')
      .toUpperCase();
  };

  return `#${f(5)}${f(3)}${f(1)}`;
}

/** Hexadécimal vers teinte et saturation, pour replacer la poignée. */
function hexToHs(hex: string): { h: number; s: number } {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const d = max - min;

  if (d === 0) {
    return { h: 0, s: 0 };
  }

  const h =
    max === r ? ((g - b) / d + (g < b ? 6 : 0)) * 60
    : max === g ? ((b - r) / d + 2) * 60
    : ((r - g) / d + 4) * 60;

  return { h, s: max === 0 ? 0 : d / max };
}
