"use client";

import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

type GalleryView = {
  id: string;
  status: string;
  photoLimit: number;
  permiteAltaResolucao: boolean;
  permiteVisualizacaoBaixa: boolean;
  blocked: boolean;
};

type PhotoItem = {
  id: string;
  src: string;
  alt: string;
  width: number;
  height: number;
};

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export default function PortalGalleryPage() {
  const params = useParams<{ id: string }>();
  const id = typeof params.id === "string" ? params.id : params.id?.[0] ?? "";
  const [data, setData] = useState<GalleryView | null>(null);
  const [photos, setPhotos] = useState<PhotoItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    const [gRes, pRes] = await Promise.all([
      fetch(`${API}/v1/portal/galleries/${id}`, { credentials: "include" }),
      fetch(`${API}/v1/portal/galleries/${id}/photos`, { credentials: "include" }),
    ]);
    if (!gRes.ok) {
      setError(`Falha (${gRes.status})`);
      return;
    }
    setData((await gRes.json()) as GalleryView);
    if (pRes.ok) setPhotos((await pRes.json()) as PhotoItem[]);
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  if (!data) {
    return (
      <main className="mx-auto max-w-3xl px-6 py-12">
        <p className="text-neutral-600">{error ?? "Carregando galeria…"}</p>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-12">
      <h1 className="font-[family-name:var(--font-display)] text-3xl tracking-tight">Galeria</h1>
      <p className="mt-2 text-neutral-600">
        {data.status} · limite {data.photoLimit} favoritas
      </p>
      {data.blocked ? (
        <p className="mt-6 border border-amber-300 bg-amber-50 p-4 text-sm text-amber-950">
          Galeria em baixa resolução — há parcela pendente (RN-ENT-032). Regularize o pagamento para
          liberar o download em alta.
        </p>
      ) : null}

      {/* DOM primeiro: cada foto e um &lt;img&gt; real (RN-FRT-003). */}
      <ul className="mt-10 grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4">
        {photos.map((p) => (
          <li key={p.id}>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={p.src}
              alt={p.alt}
              width={p.width}
              height={p.height}
              className="aspect-[3/4] w-full object-cover bg-neutral-200"
              loading="lazy"
            />
          </li>
        ))}
        {photos.length === 0 ? (
          <li className="col-span-full py-8 text-sm text-neutral-500">Nenhuma foto ainda.</li>
        ) : null}
      </ul>
    </main>
  );
}
