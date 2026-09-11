import localFont from "next/font/local";
import { GeistSans } from "geist/font/sans";

/**
 * Quatro pares tipograficos self-hosted (docs/16 §6).
 * Satoshi (expressivo/corpo): Inter Tight no lugar ate arquivo local licenciado.
 */
export const fraunces = localFont({
  src: [
    {
      path: "../node_modules/@fontsource-variable/fraunces/files/fraunces-latin-wght-normal.woff2",
      weight: "100 900",
      style: "normal",
    },
  ],
  variable: "--font-pair-serifado-display",
  display: "swap",
});

export const interTight = localFont({
  src: [
    {
      path: "../node_modules/@fontsource-variable/inter-tight/files/inter-tight-latin-wght-normal.woff2",
      weight: "100 900",
      style: "normal",
    },
  ],
  variable: "--font-pair-serifado-body",
  display: "swap",
});

export const instrumentSerif = localFont({
  src: [
    {
      path: "../node_modules/@fontsource/instrument-serif/files/instrument-serif-latin-400-normal.woff2",
      weight: "400",
      style: "normal",
    },
  ],
  variable: "--font-pair-alto-display",
  display: "swap",
});

export const bricolage = localFont({
  src: [
    {
      path: "../node_modules/@fontsource-variable/bricolage-grotesque/files/bricolage-grotesque-latin-wght-normal.woff2",
      weight: "200 800",
      style: "normal",
    },
  ],
  variable: "--font-pair-expressivo-display",
  display: "swap",
});

export const geistSans = GeistSans;

export type TypePair = "serifado" | "alto-contraste" | "expressivo" | "neutro";

export function typePairClassNames(pair: TypePair = "alto-contraste") {
  const base = [
    fraunces.variable,
    interTight.variable,
    instrumentSerif.variable,
    bricolage.variable,
    geistSans.variable,
  ].join(" ");

  switch (pair) {
    case "serifado":
      return `${base} [font-family:var(--font-pair-serifado-body)] [&_[data-display]]:font-[family-name:var(--font-pair-serifado-display)]`;
    case "expressivo":
      return `${base} [font-family:var(--font-pair-serifado-body)] [&_[data-display]]:font-[family-name:var(--font-pair-expressivo-display)]`;
    case "neutro":
      return `${base} ${geistSans.className}`;
    case "alto-contraste":
    default:
      return `${base} ${geistSans.className} [&_[data-display]]:font-[family-name:var(--font-pair-alto-display)]`;
  }
}
