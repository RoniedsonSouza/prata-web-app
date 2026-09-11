"use client";

import { useCallback, useEffect, useState } from "react";

type Summary = {
  aReceber: number;
  liquidado: number;
  repassado: number;
  kycBloqueandoRepasse: boolean;
};

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export default function StudioFinancePage() {
  const [token, setToken] = useState("");
  const [summary, setSummary] = useState<Summary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setToken(window.localStorage.getItem("prata_access_token") ?? "");
  }, []);

  const load = useCallback(async () => {
    if (!token) return;
    const res = await fetch(`${API}/v1/studio/finance/summary`, {
      headers: { Authorization: `Bearer ${token}` },
      credentials: "include",
    });
    if (!res.ok) {
      setError(`Falha (${res.status})`);
      return;
    }
    setSummary((await res.json()) as Summary);
  }, [token]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <main className="mx-auto max-w-3xl px-6 py-12">
      <h1 className="font-[family-name:var(--font-display)] text-3xl tracking-tight">Financeiro</h1>
      <p className="mt-2 text-neutral-600">Três estados do dinheiro — separados de propósito.</p>
      {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}
      {summary?.kycBloqueandoRepasse ? (
        <p className="mt-4 border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          KYC pendente bloqueando repasse (RN-FIN-030). Conclua a verificação no PSP.
        </p>
      ) : null}
      <div className="mt-10 grid gap-6 sm:grid-cols-3">
        <Column title="A receber" value={summary?.aReceber} />
        <Column title="Liquidado" value={summary?.liquidado} />
        <Column title="Repassado" value={summary?.repassado} />
      </div>
    </main>
  );
}

function Column({ title, value }: { title: string; value?: number }) {
  return (
    <section className="border-t border-neutral-900 pt-4">
      <h2 className="text-sm uppercase tracking-wide text-neutral-500">{title}</h2>
      <p className="mt-2 text-2xl tabular-nums">
        {value == null ? "—" : `R$ ${Number(value).toFixed(2)}`}
      </p>
    </section>
  );
}
