"use client";

import { useCallback, useEffect, useState } from "react";

type OrderRow = {
  id: string;
  status: string;
  intendedDate: string;
  total: number;
  createdAt: string;
};

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export default function StudioOrdersPage() {
  const [orders, setOrders] = useState<OrderRow[]>([]);
  const [status, setStatus] = useState("");
  const [token, setToken] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [waUrl, setWaUrl] = useState<string | null>(null);
  const [fichaUrl, setFichaUrl] = useState<string | null>(null);

  useEffect(() => {
    setToken(window.localStorage.getItem("prata_access_token") ?? "");
  }, []);

  const load = useCallback(async () => {
    setError(null);
    const qs = status ? `?status=${encodeURIComponent(status)}` : "";
    const res = await fetch(`${API}/v1/studio/orders${qs}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      credentials: "include",
    });
    if (!res.ok) {
      setError(`Falha ao listar (${res.status})`);
      return;
    }
    const data = (await res.json()) as OrderRow[];
    setOrders(data);
  }, [status, token]);

  useEffect(() => {
    if (token) void load();
  }, [load, token]);

  async function waLink(id: string) {
    const res = await fetch(`${API}/v1/studio/orders/${id}/wa-link`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    if (!res.ok) {
      setError("Cliente sem WhatsApp ou pedido ausente");
      return;
    }
    const data = (await res.json()) as { url: string };
    setWaUrl(data.url);
  }

  async function fichaLink(id: string) {
    const res = await fetch(`${API}/v1/studio/orders/${id}/briefing/direction-sheet-link`, {
      method: "POST",
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    if (!res.ok) {
      setError("Nao foi possivel emitir URL assinada da ficha");
      return;
    }
    const data = (await res.json()) as { url: string };
    const absolute = data.url.startsWith("http") ? data.url : `${API}${data.url}`;
    setFichaUrl(absolute);
    window.open(absolute, "_blank", "noopener,noreferrer");
  }

  const pendencias = orders.filter((o) =>
    ["Enviado", "EmAnalise", "OrcamentoEnviado", "EmEspera"].includes(o.status)
  );

  return (
    <main className="mx-auto max-w-4xl px-6 py-12">
      <h1 className="font-[family-name:var(--font-display)] text-3xl tracking-tight">Pedidos</h1>
      <p className="mt-2 max-w-xl text-neutral-600">
        Lista acionavel do comercial — analisar, orcar e abrir WhatsApp sem coletar briefing por la.
      </p>

      <div className="mt-8 flex flex-wrap items-end gap-4">
        <label className="flex flex-col gap-1 text-sm">
          Status
          <select
            className="border border-neutral-300 bg-white px-3 py-2"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">Todos</option>
            <option value="Enviado">Enviado</option>
            <option value="EmAnalise">Em analise</option>
            <option value="OrcamentoEnviado">Orcamento enviado</option>
            <option value="Aprovado">Aprovado</option>
            <option value="EmEspera">Em espera</option>
          </select>
        </label>
        <button
          type="button"
          onClick={() => void load()}
          className="border border-neutral-900 px-4 py-2 text-sm"
        >
          Atualizar
        </button>
      </div>

      {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}
      {waUrl ? (
        <p className="mt-4 text-sm">
          Link WhatsApp:{" "}
          <a className="underline" href={waUrl} target="_blank" rel="noreferrer">
            {waUrl}
          </a>
        </p>
      ) : null}
      {fichaUrl ? (
        <p className="mt-2 text-sm">
          Ficha (URL assinada TTL curto):{" "}
          <a className="underline" href={fichaUrl} target="_blank" rel="noreferrer">
            abrir PDF
          </a>
        </p>
      ) : null}

      {pendencias.length > 0 ? (
        <section className="mt-8 border border-amber-200 bg-amber-50 p-4">
          <h2 className="font-medium">Pendencias acionaveis</h2>
          <ul className="mt-2 space-y-2 text-sm">
            {pendencias.map((o) => (
              <li key={o.id} className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  {o.status} · {o.intendedDate}
                </span>
                <button
                  type="button"
                  className="underline"
                  onClick={() => void waLink(o.id)}
                >
                  wa.me
                </button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <ul className="mt-8 divide-y divide-neutral-200 border-t border-neutral-200">
        {orders.map((o) => (
          <li key={o.id} className="flex flex-wrap items-center justify-between gap-3 py-4">
            <div>
              <p className="font-medium">
                <a className="underline" href={`/studio/orders/${o.id}`}>
                  {o.status}
                </a>
              </p>
              <p className="text-sm text-neutral-600">
                {o.intendedDate} · R$ {Number(o.total).toFixed(2)}
              </p>
              <p className="font-mono text-xs text-neutral-500">{o.id}</p>
            </div>
            <div className="flex gap-2">
              <a
                className="border border-neutral-400 px-3 py-1.5 text-sm"
                href={`/studio/orders/${o.id}`}
              >
                Orçar
              </a>
              <button
                type="button"
                className="border border-neutral-400 px-3 py-1.5 text-sm"
                onClick={() => void fichaLink(o.id)}
              >
                Ficha
              </button>
              <button
                type="button"
                className="border border-neutral-900 bg-neutral-900 px-3 py-1.5 text-sm text-white"
                onClick={() => void waLink(o.id)}
              >
                wa.me
              </button>
            </div>
          </li>
        ))}
        {orders.length === 0 ? (
          <li className="py-8 text-sm text-neutral-500">Nenhum pedido neste filtro.</li>
        ) : null}
      </ul>
    </main>
  );
}
