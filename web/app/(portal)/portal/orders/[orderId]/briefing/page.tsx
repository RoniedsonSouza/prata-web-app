"use client";

import { useParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { evaluateVisibleWhen } from "@/lib/visible-when";

type Option = { code: string; label: string; isSuggestedChip?: boolean };
type Question = {
  code: string;
  prompt: string;
  type: string;
  block: string;
  isRequired: boolean;
  isSensitive: boolean;
  visibleWhen?: string | null;
  visible?: boolean;
  suggestSelfieTest?: boolean;
  options: Option[];
  answer?: string | null;
};

type BriefingPayload = {
  orderId: string;
  consentActive: boolean;
  purposeText: string;
  progress: {
    total: number;
    answered: number;
    byBlock: Record<string, { total: number; answered: number }>;
  };
  questions: Question[];
};

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

function parseAnswer(raw?: string | null): Record<string, unknown> {
  if (!raw) return {};
  try {
    return JSON.parse(raw) as Record<string, unknown>;
  } catch {
    return {};
  }
}

export default function PortalBriefingPage() {
  const params = useParams<{ orderId: string }>();
  const orderId = typeof params.orderId === "string" ? params.orderId : params.orderId?.[0] ?? "";

  const [data, setData] = useState<BriefingPayload | null>(null);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState<string | null>(null);
  const [blockIndex, setBlockIndex] = useState(0);

  const load = useCallback(async () => {
    if (!orderId) return;
    setError(null);
    const res = await fetch(`${API}/v1/portal/orders/${orderId}/briefing`, {
      credentials: "include",
    });
    if (!res.ok) {
      setError(`Falha ao carregar (${res.status})`);
      return;
    }
    const payload = (await res.json()) as BriefingPayload;
    setData(payload);
    const map: Record<string, string> = {};
    for (const q of payload.questions ?? []) {
      if (q.answer) map[q.code] = q.answer;
    }
    setAnswers(map);
  }, [orderId]);

  useEffect(() => {
    void load();
  }, [load]);

  const answerMap = useMemo(() => {
    const m: Record<string, Record<string, unknown>> = {};
    for (const [code, raw] of Object.entries(answers)) m[code] = parseAnswer(raw);
    return m;
  }, [answers]);

  const visibleQuestions = useMemo(() => {
    if (!data) return [];
    return (data.questions ?? []).filter((q) => evaluateVisibleWhen(q.visibleWhen, answerMap));
  }, [data, answerMap]);

  const blocks = useMemo(() => {
    const order = ["A", "B", "C", "D", "E"];
    const present = [...new Set(visibleQuestions.map((q) => q.block))];
    return order.filter((b) => present.includes(b)).concat(present.filter((b) => !order.includes(b)));
  }, [visibleQuestions]);

  const currentBlock = blocks[blockIndex] ?? blocks[0];
  const blockQuestions = visibleQuestions.filter((q) => q.block === currentBlock);

  async function saveAnswer(code: string, valueJson: string) {
    setAnswers((prev) => ({ ...prev, [code]: valueJson }));
    setSaving(code);
    const res = await fetch(`${API}/v1/portal/orders/${orderId}/briefing/answers/${code}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({ valueJson }),
    });
    setSaving(null);
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      setError((body as { title?: string }).title ?? `Falha ao salvar ${code}`);
    }
  }

  async function grantConsent() {
    const res = await fetch(`${API}/v1/portal/orders/${orderId}/briefing/consent`, {
      method: "POST",
      credentials: "include",
    });
    if (!res.ok) {
      setError("Nao foi possivel registrar o consentimento");
      return;
    }
    await load();
  }

  async function submitOrder() {
    const res = await fetch(`${API}/v1/portal/orders/${orderId}/submit`, {
      method: "POST",
      credentials: "include",
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      setError((body as { title?: string }).title ?? "Nao foi possivel enviar o pedido");
      return;
    }
    setError(null);
    alert("Pedido enviado ao estudio.");
  }

  if (!data) {
    return (
      <main className="mx-auto max-w-2xl px-6 py-12">
        <p className="text-neutral-600">{error ?? "Carregando briefing…"}</p>
      </main>
    );
  }

  const progressPct =
    data.progress.total === 0 ? 0 : Math.round((data.progress.answered / data.progress.total) * 100);

  return (
    <main className="mx-auto max-w-2xl px-6 py-12">
      <h1 className="font-[family-name:var(--font-display)] text-3xl tracking-tight">Briefing</h1>
      <p className="mt-2 text-neutral-600">
        Retome quando quiser — progresso por bloco. Autosave a cada resposta.
      </p>

      <div className="mt-6 h-2 w-full bg-neutral-200">
        <div className="h-2 bg-neutral-900 transition-all" style={{ width: `${progressPct}%` }} />
      </div>
      <p className="mt-2 text-sm text-neutral-500">
        {data.progress.answered}/{data.progress.total} visiveis · bloco {currentBlock}
      </p>

      <nav className="mt-6 flex flex-wrap gap-2">
        {blocks.map((b, i) => (
          <button
            key={b}
            type="button"
            onClick={() => setBlockIndex(i)}
            className={`border px-3 py-1.5 text-sm ${
              b === currentBlock ? "border-neutral-900 bg-neutral-900 text-white" : "border-neutral-300"
            }`}
          >
            {b}
            {data.progress.byBlock[b]
              ? ` (${data.progress.byBlock[b].answered}/${data.progress.byBlock[b].total})`
              : ""}
          </button>
        ))}
      </nav>

      {currentBlock === "B" && !data.consentActive ? (
        <section className="mt-8 border border-neutral-300 bg-neutral-50 p-4">
          <p className="text-sm leading-relaxed">{data.purposeText}</p>
          <button
            type="button"
            className="mt-4 border border-neutral-900 bg-neutral-900 px-4 py-2 text-sm text-white"
            onClick={() => void grantConsent()}
          >
            Concordo em responder o bloco opcional
          </button>
        </section>
      ) : null}

      {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}

      <ul className="mt-8 space-y-8">
        {blockQuestions.map((q) => {
          if (q.isSensitive && currentBlock === "B" && !data.consentActive) return null;
          const parsed = parseAnswer(answers[q.code]);
          const selfie = BriefingUxHintsClient(q.code, answers[q.code]);
          return (
            <li key={q.code}>
              <label className="block text-sm font-medium">
                {q.code} · {q.prompt}
                {q.isRequired ? " *" : ""}
              </label>
              {q.type === "SingleChoice" || q.type === "MultiChoice" ? (
                <div className="mt-2 flex flex-wrap gap-2">
                  {q.options.map((o) => (
                    <button
                      key={o.code}
                      type="button"
                      className={`border px-3 py-1.5 text-sm ${
                        parsed.option === o.code || parsed.value === o.code
                          ? "border-neutral-900 bg-neutral-900 text-white"
                          : "border-neutral-300"
                      }`}
                      onClick={() =>
                        void saveAnswer(q.code, JSON.stringify({ option: o.code, value: o.code }))
                      }
                    >
                      {o.label}
                    </button>
                  ))}
                </div>
              ) : q.type === "Scale" ? (
                <div className="mt-2 flex gap-2">
                  {[1, 2, 3, 4, 5].map((n) => (
                    <button
                      key={n}
                      type="button"
                      className={`h-10 w-10 border text-sm ${
                        Number(parsed.value) === n
                          ? "border-neutral-900 bg-neutral-900 text-white"
                          : "border-neutral-300"
                      }`}
                      onClick={() => void saveAnswer(q.code, JSON.stringify({ value: n }))}
                    >
                      {n}
                    </button>
                  ))}
                </div>
              ) : q.type === "Upload" ? (
                <UploadField
                  code={q.code}
                  orderId={orderId}
                  existing={parsed}
                  onSaved={(json) => void saveAnswer(q.code, json)}
                  onError={setError}
                />
              ) : q.type === "Url" ? (
                <textarea
                  className="mt-2 w-full border border-neutral-300 px-3 py-2 text-sm"
                  rows={2}
                  placeholder="https://…"
                  defaultValue={
                    Array.isArray(parsed.urls)
                      ? (parsed.urls as string[]).join("\n")
                      : String(parsed.text ?? "")
                  }
                  onBlur={(e) => {
                    const urls = e.target.value
                      .split("\n")
                      .map((u) => u.trim())
                      .filter(Boolean);
                    void saveAnswer(q.code, JSON.stringify({ urls }));
                  }}
                />
              ) : (
                <textarea
                  className="mt-2 w-full border border-neutral-300 px-3 py-2 text-sm"
                  rows={3}
                  defaultValue={String(parsed.text ?? parsed.value ?? "")}
                  onBlur={(e) =>
                    void saveAnswer(q.code, JSON.stringify({ text: e.target.value, value: e.target.value }))
                  }
                />
              )}
              {selfie ? (
                <p className="mt-2 text-sm text-amber-800">
                  Sugestao: tire duas selfies (um lado de cada vez) e escolha qual prefere — ajuda o fotografo.
                </p>
              ) : null}
              {saving === q.code ? <p className="mt-1 text-xs text-neutral-500">Salvando…</p> : null}
            </li>
          );
        })}
      </ul>

      <div className="mt-10 flex flex-wrap gap-3">
        <button
          type="button"
          className="border border-neutral-400 px-4 py-2 text-sm"
          disabled={blockIndex === 0}
          onClick={() => setBlockIndex((i) => Math.max(0, i - 1))}
        >
          Bloco anterior
        </button>
        <button
          type="button"
          className="border border-neutral-400 px-4 py-2 text-sm"
          disabled={blockIndex >= blocks.length - 1}
          onClick={() => setBlockIndex((i) => Math.min(blocks.length - 1, i + 1))}
        >
          Proximo bloco
        </button>
        <button
          type="button"
          className="border border-neutral-900 bg-neutral-900 px-4 py-2 text-sm text-white"
          onClick={() => void submitOrder()}
        >
          Enviar pedido
        </button>
      </div>
    </main>
  );
}

function BriefingUxHintsClient(code: string, valueJson?: string) {
  if (code !== "B2" || !valueJson) return false;
  try {
    const v = JSON.parse(valueJson) as { option?: string; value?: string };
    return (v.option ?? v.value)?.toUpperCase() === "NAO_SEI";
  } catch {
    return false;
  }
}

function UploadField({
  code,
  orderId,
  existing,
  onSaved,
  onError,
}: {
  code: string;
  orderId: string;
  existing: Record<string, unknown>;
  onSaved: (json: string) => void;
  onError: (msg: string) => void;
}) {
  const [caption, setCaption] = useState(
    typeof existing.text === "string" ? existing.text : ""
  );
  const [busy, setBusy] = useState(false);
  const assets = Array.isArray(existing.assets)
    ? (existing.assets as { key?: string; name?: string }[])
    : [];

  async function onFile(file: File | null) {
    if (!file) return;
    setBusy(true);
    try {
      const signedRes = await fetch(`${API}/v1/portal/orders/${orderId}/briefing/uploads`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ contentType: file.type || "image/jpeg" }),
      });
      if (!signedRes.ok) {
        onError(`Falha ao pedir upload (${signedRes.status})`);
        return;
      }
      const signed = (await signedRes.json()) as {
        objectKey: string;
        uploadUrl: string;
      };

      // Em Dev a URL e fake; ainda assim persistimos a chave na resposta.
      if (!signed.uploadUrl.includes("upload.local.dev")) {
        const put = await fetch(signed.uploadUrl, {
          method: "PUT",
          headers: { "Content-Type": file.type || "image/jpeg" },
          body: file,
        });
        if (!put.ok) {
          onError(`Falha no upload do arquivo (${put.status})`);
          return;
        }
      }

      const nextAssets = [
        ...assets.filter((a) => a.key !== signed.objectKey),
        { key: signed.objectKey, name: file.name || `${code}.jpg` },
      ];
      onSaved(JSON.stringify({ assets: nextAssets, text: caption }));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-2 space-y-2">
      <input
        type="file"
        accept="image/*"
        capture="environment"
        className="block w-full text-sm"
        disabled={busy}
        onChange={(e) => void onFile(e.target.files?.[0] ?? null)}
      />
      {(code === "B8" || code === "D2") && (
        <input
          className="w-full border border-neutral-300 px-3 py-2 text-sm"
          placeholder={code === "B8" ? "Por que gosta desta foto?" : "Legenda opcional"}
          value={caption}
          onChange={(e) => setCaption(e.target.value)}
          onBlur={() => {
            if (assets.length > 0)
              onSaved(JSON.stringify({ assets, text: caption }));
          }}
        />
      )}
      {assets.length > 0 ? (
        <p className="text-xs text-neutral-500">
          {assets.length} arquivo(s) · {assets.map((a) => a.name ?? a.key).join(", ")}
        </p>
      ) : null}
      {busy ? <p className="text-xs text-neutral-500">Enviando…</p> : null}
    </div>
  );
}
