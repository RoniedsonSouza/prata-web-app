"use client";

import { ImmersiveLayer } from "@/components/immersive-layer";

/** Temas cinema/imersivo — WebGL lazy so na rota publica (RN-FRT-006). */
export function PublicImmersiveShell() {
  return <ImmersiveLayer textureUrls={[]} />;
}
