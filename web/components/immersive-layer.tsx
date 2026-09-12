"use client";

import { useEffect, useRef, useState } from "react";

/**
 * Camada WebGL dos temas cinema/imersivo (E4).
 * DOM permanece a fonte da verdade (RN-FRT-003); canvas so monta apos LCP e se passar RN-FRT-004.
 */
export function ImmersiveLayer({ textureUrls }: { textureUrls: string[] }) {
  const hostRef = useRef<HTMLDivElement>(null);
  const [active, setActive] = useState(false);

  useEffect(() => {
    if (typeof window === "undefined") return;

    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const saveData = (navigator as Navigator & { connection?: { saveData?: boolean } }).connection
      ?.saveData;
    const mem = (navigator as Navigator & { deviceMemory?: number }).deviceMemory ?? 8;
    const cores = navigator.hardwareConcurrency ?? 8;

    const canvas = document.createElement("canvas");
    const gl = canvas.getContext("webgl2");
    const ok =
      !reduced && !saveData && mem >= 4 && cores >= 4 && gl != null && textureUrls.length > 0;

    if (!ok) {
      setActive(false);
      return;
    }

    let cancelled = false;
    const boot = () => {
      if (cancelled) return;
      void import("./immersive-webgl")
        .then((mod) => {
          if (cancelled || !hostRef.current) return;
          setActive(true);
          mod.mountImmersive(hostRef.current, textureUrls);
        })
        .catch(() => {
          // RN-FRT-007 — degrada em silencio.
          setActive(false);
        });
    };

    const w = window as Window & {
      requestIdleCallback?: (cb: () => void) => number;
    };
    if (typeof w.requestIdleCallback === "function") {
      w.requestIdleCallback(boot);
    } else {
      globalThis.setTimeout(boot, 1200);
    }

    return () => {
      cancelled = true;
    };
  }, [textureUrls]);

  return (
    <div
      ref={hostRef}
      className="pointer-events-none absolute inset-0 -z-10"
      aria-hidden
      data-immersive={active ? "on" : "off"}
    />
  );
}
