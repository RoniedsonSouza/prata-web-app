"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

type OrderItem = {
  id: string;
  kind: string;
  nameSnapshot: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
};

type OrderDetail = {
  id: string;
  status: string;
  serviceTypeId: string;
  intendedDate: string;
  subtotal: number;
  discount: number;
  discountKind?: string | null;
  discountPercent?: number | null;
  discountFixed?: number | null;
  total: number;
  items: OrderItem[];
  quote?: { version: number; validoAte: string; total: number } | null;
};

type CatalogOptions = {
  packages: { id: string; name: string; price?: number | null }[];
  addons: { id: string; name: string; price: number }[];
};

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export default function StudioOrderDetailPage() {
  const params = useParams<{ id: string }>();
  const id = typeof params.id === "string" ? params.id : params.id?.[0] ?? "";
  const [token, setToken] = useState("");
  const [order, setOrder] = useState<OrderDetail | null>(null);
  const [catalog, setCatalog] = useState<CatalogOptions | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [packageId, setPackageId] = useState("");
  const [addonId, setAddonId] = useState("");
  const [discountKind, setDiscountKind] = useState<"Fixed" | "Percent">("Percent");
  const [discountValue, setDiscountValue] = useState("10");
  const [validadeDias, setValidadeDias] = useState("7");
  const [holdReason, setHoldReason] = useState("");
  const [refuseReason, setRefuseReason] = useState("");

  useEffect(() => {
    setToken(window.localStorage.getItem("prata_access_token") ?? "");
  }, []);

  const headers = useCallback(
    () => ({
      Authorization: token ? `Bearer ${token}` : "",
      "Content-Type": "application/json",
    }),
    [token]
  );

  const load = useCallback(async () => {
    if (!id || !token) return;
    setError(null);
    const [orderRes, catRes] = await Promise.all([
      fetch(`${API}/v1/studio/orders/${id}`, { headers: headers(), credentials: "include" }),
      fetch(`${API}/v1/studio/orders/${id}/catalog-options`, {
        headers: headers(),
        credentials: "include",
      }),
    ]);
    if (!orderRes.ok) {
      setError(`Falha ao carregar pedido (${orderRes.status})`);
      return;
    }
    const data = (await orderRes.json()) as OrderDetail;
    setOrder(data);
    if (catRes.ok) {
      const cat = (await catRes.json()) as CatalogOptions;
      setCatalog(cat);
      if (!packageId && cat.packages[0]) setPackageId(cat.packages[0].id);
      if (!addonId && cat.addons[0]) setAddonId(cat.addons[0].id);
    }
  }, [addonId, headers, id, packageId, token]);

  useEffect(() => {
    void load();
  }, [load]);

  async function post(path: string, body?: unknown) {
    const res = await fetch(`${API}${path}`, {
      method: "POST",
      headers: headers(),
      credentials: "include",
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      setError((err as { title?: string }).title ?? `Falha (${res.status})`);
      return false;
    }
    await load();
    return true;
  }

  async function del(path: string) {
    const res = await fetch(`${API}${path}`, {
      method: "DELETE",
      headers: headers(),
      credentials: "include",
    });
    if (!res.ok) {
      setError(`Falha ao remover (${res.status})`);
      return;
    }
    await load();
  }

  if (!order) {
    return (
      <main className="mx-auto max-w-3xl px-6 py-12">
        <p className="text-neutral-600">{error ?? "Carregando…"}</p>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-3xl px-6 py-12">
      <p className="text-sm text-neutral-500">
        <Link href="/studio/orders" className="underline">
          ← Pedidos
        </Link>
      </p>
      <h1 className="mt-4 font-[family-name:var(--font-display)] text-3xl tracking-tight">
        Orçamento
      </h1>
      <p className="mt-2 text-neutral-600">
        {order.status} · {order.intendedDate} · R$ {Number(order.total).toFixed(2)}
      </p>
      <p className="font-mono text-xs text-neutral-500">{order.id}</p>

      {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}

      <section className="mt-8 border-t border-neutral-200 pt-6">
        <h2 className="font-medium">Itens</h2>
        <ul className="mt-3 divide-y divide-neutral-200">
          {order.items.map((item) => (
            <li key={item.id} className="flex items-center justify-between gap-3 py-3 text-sm">
              <span>
                {item.kind} · {item.nameSnapshot} × {item.quantity}
              </span>
              <span className="flex items-center gap-3">
                R$ {Number(item.lineTotal).toFixed(2)}
                <button
                  type="button"
                  className="underline"
                  onClick={() => void del(`/v1/studio/orders/${id}/items/${item.id}`)}
                >
                  remover
                </button>
              </span>
            </li>
          ))}
          {order.items.length === 0 ? (
            <li className="py-3 text-sm text-neutral-500">Nenhum item ainda.</li>
          ) : null}
        </ul>
        <p className="mt-2 text-sm text-neutral-600">
          Subtotal R$ {Number(order.subtotal).toFixed(2)} · Desconto R${" "}
          {Number(order.discount).toFixed(2)} · Total R$ {Number(order.total).toFixed(2)}
        </p>
      </section>

      <section className="mt-8 grid gap-4 border-t border-neutral-200 pt-6 sm:grid-cols-2">
        <div>
          <h2 className="font-medium">Pacote</h2>
          <select
            className="mt-2 w-full border border-neutral-300 px-3 py-2 text-sm"
            value={packageId}
            onChange={(e) => setPackageId(e.target.value)}
          >
            {(catalog?.packages ?? []).map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
                {p.price != null ? ` — R$ ${Number(p.price).toFixed(2)}` : ""}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="mt-2 border border-neutral-900 px-3 py-1.5 text-sm"
            onClick={() => void post(`/v1/studio/orders/${id}/items/package`, { packageId })}
          >
            Adicionar pacote
          </button>
        </div>
        <div>
          <h2 className="font-medium">Adicional</h2>
          <select
            className="mt-2 w-full border border-neutral-300 px-3 py-2 text-sm"
            value={addonId}
            onChange={(e) => setAddonId(e.target.value)}
          >
            {(catalog?.addons ?? []).map((a) => (
              <option key={a.id} value={a.id}>
                {a.name} — R$ {Number(a.price).toFixed(2)}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="mt-2 border border-neutral-900 px-3 py-1.5 text-sm"
            onClick={() =>
              void post(`/v1/studio/orders/${id}/items/addon`, { addonId, quantity: 1 })
            }
          >
            Adicionar adicional
          </button>
        </div>
      </section>

      <section className="mt-8 border-t border-neutral-200 pt-6">
        <h2 className="font-medium">Desconto e validade</h2>
        <div className="mt-3 flex flex-wrap items-end gap-3">
          <label className="text-sm">
            Tipo
            <select
              className="mt-1 block border border-neutral-300 px-3 py-2"
              value={discountKind}
              onChange={(e) => setDiscountKind(e.target.value as "Fixed" | "Percent")}
            >
              <option value="Percent">Percentual</option>
              <option value="Fixed">Fixo (R$)</option>
            </select>
          </label>
          <label className="text-sm">
            Valor
            <input
              className="mt-1 block w-28 border border-neutral-300 px-3 py-2"
              value={discountValue}
              onChange={(e) => setDiscountValue(e.target.value)}
            />
          </label>
          <button
            type="button"
            className="border border-neutral-900 px-3 py-2 text-sm"
            onClick={() => {
              const n = Number(discountValue);
              void post(`/v1/studio/orders/${id}/discount`, {
                kind: discountKind,
                fixedAmount: discountKind === "Fixed" ? n : null,
                percent: discountKind === "Percent" ? n : null,
              });
            }}
          >
            Aplicar desconto
          </button>
          <button
            type="button"
            className="border border-neutral-400 px-3 py-2 text-sm"
            onClick={() => void del(`/v1/studio/orders/${id}/discount`)}
          >
            Limpar
          </button>
        </div>
        <label className="mt-4 block text-sm">
          Validade (dias)
          <input
            className="mt-1 block w-24 border border-neutral-300 px-3 py-2"
            value={validadeDias}
            onChange={(e) => setValidadeDias(e.target.value)}
          />
        </label>
        {order.quote ? (
          <p className="mt-2 text-sm text-neutral-600">
            Orçamento v{order.quote.version} válido até {order.quote.validoAte}
          </p>
        ) : null}
      </section>

      <section className="mt-8 flex flex-wrap gap-2 border-t border-neutral-200 pt-6">
        <button
          type="button"
          className="border border-neutral-400 px-3 py-2 text-sm"
          onClick={() => void post(`/v1/studio/orders/${id}/analyze`)}
        >
          Analisar
        </button>
        <button
          type="button"
          className="border border-neutral-900 bg-neutral-900 px-3 py-2 text-sm text-white"
          onClick={() =>
            void post(`/v1/studio/orders/${id}/send-quote`, {
              validadeDias: Number(validadeDias) || 7,
            })
          }
        >
          Enviar orçamento
        </button>
        <button
          type="button"
          className="border border-neutral-400 px-3 py-2 text-sm"
          onClick={() => {
            const motivo = holdReason || "Aguardando cliente";
            void post(`/v1/studio/orders/${id}/hold`, { motivo });
          }}
        >
          Em espera
        </button>
        <input
          className="border border-neutral-300 px-2 py-1 text-sm"
          placeholder="Motivo espera"
          value={holdReason}
          onChange={(e) => setHoldReason(e.target.value)}
        />
        <button
          type="button"
          className="border border-red-700 px-3 py-2 text-sm text-red-800"
          onClick={() => {
            const motivo = refuseReason || "Sem disponibilidade";
            void post(`/v1/studio/orders/${id}/refuse`, { motivo });
          }}
        >
          Recusar
        </button>
        <input
          className="border border-neutral-300 px-2 py-1 text-sm"
          placeholder="Motivo recusa"
          value={refuseReason}
          onChange={(e) => setRefuseReason(e.target.value)}
        />
      </section>
    </main>
  );
}
