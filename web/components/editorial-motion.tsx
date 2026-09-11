"use client";

import { useEffect } from "react";

/**
 * Tema editorial (E1): GSAP reveal + Lenis sem syncTouch (RN-FRT-005).
 * Carregado so no client e apos LCP; prefers-reduced-motion desliga.
 */
export function EditorialMotion() {
  useEffect(() => {
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduced) {
      return;
    }

    let cleanup = () => {};

    void (async () => {
      const [{ default: gsap }, { ScrollTrigger }, { default: Lenis }] = await Promise.all([
        import("gsap"),
        import("gsap/ScrollTrigger"),
        import("lenis"),
      ]);

      gsap.registerPlugin(ScrollTrigger);

      const lenis = new Lenis({
        // syncTouch propositalmente omitido — RN-FRT-005 / docs/16
        smoothWheel: true,
      });

      lenis.on("scroll", ScrollTrigger.update);
      const ticker = (time: number) => {
        lenis.raf(time * 1000);
      };
      gsap.ticker.add(ticker);
      gsap.ticker.lagSmoothing(0);

      gsap.utils.toArray<HTMLElement>("[data-reveal]").forEach((el) => {
        gsap.fromTo(
          el,
          { opacity: 0, y: 24 },
          {
            opacity: 1,
            y: 0,
            duration: 0.9,
            ease: "power2.out",
            scrollTrigger: { trigger: el, start: "top 85%" },
          },
        );
      });

      const parallax = document.querySelector<HTMLElement>("[data-parallax]");
      if (parallax) {
        gsap.to(parallax, {
          yPercent: 12,
          ease: "none",
          scrollTrigger: {
            trigger: parallax,
            start: "top bottom",
            end: "bottom top",
            scrub: true,
          },
        });
      }

      cleanup = () => {
        gsap.ticker.remove(ticker);
        lenis.destroy();
        ScrollTrigger.getAll().forEach((t) => t.kill());
      };
    })();

    return () => cleanup();
  }, []);

  return null;
}
