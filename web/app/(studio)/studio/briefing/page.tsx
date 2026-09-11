"use client";

import { useCallback, useEffect, useState } from "react";

type TemplateRow = {
  id: string;
  name: string;
  serviceTypeId: string;
  version: number;
  isPublished: boolean;
  questionCount: number;
};

type QuestionRow = {
  code: string;
  prompt: string;
  type: string;
  block: string;
  sortOrder: number;
  isRequired: boolean;
  isSensitive: boolean;
  visibleWhen?: string | null;
};

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export default function BriefingTemplatesPage() {
  const [token, setToken] = useState("");
  const [templates, setTemplates] = useState<TemplateRow[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [questions, setQuestions] = useState<QuestionRow[]>([]);
  const [published, setPublished] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [draft, setDraft] = useState({
    code: "",
    prompt: "",
    type: "Text",
    block: "A",
    sortOrder: 100,
    isRequired: false,
    isSensitive: false,
    visibleWhen: "",
  });

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

  const loadList = useCallback(async () => {
    const res = await fetch(`${API}/v1/studio/briefing-templates`, { headers: headers() });
    if (!res.ok) {
      setError(`Falha ao listar (${res.status})`);
      return;
    }
    setTemplates((await res.json()) as TemplateRow[]);
  }, [headers]);

  useEffect(() => {
    if (token) void loadList();
  }, [token, loadList]);

  async function openTemplate(id: string) {
    setSelectedId(id);
    setError(null);
    const res = await fetch(`${API}/v1/studio/briefing-templates/${id}`, { headers: headers() });
    if (!res.ok) {
      setError("Template nao encontrado");
      return;
    }
    const data = (await res.json()) as {
      isPublished: boolean;
      questions: QuestionRow[];
    };
    setPublished(data.isPublished);
    setQuestions(data.questions);
  }

  async function openEdit() {
    if (!selectedId) return;
    const res = await fetch(`${API}/v1/studio/briefing-templates/${selectedId}/open-edit`, {
      method: "POST",
      headers: headers(),
    });
    if (!res.ok) {
      setError("Nao foi possivel abrir edicao");
      return;
    }
    await openTemplate(selectedId);
    await loadList();
  }

  async function newVersion() {
    if (!selectedId) return;
    const res = await fetch(`${API}/v1/studio/briefing-templates/${selectedId}/new-version`, {
      method: "POST",
      headers: headers(),
    });
    if (!res.ok) {
      setError("Falha ao criar versao");
      return;
    }
    const body = (await res.json()) as { id: string };
    await loadList();
    await openTemplate(body.id);
  }

  async function addQuestion() {
    if (!selectedId) return;
    const res = await fetch(`${API}/v1/studio/briefing-templates/${selectedId}/questions`, {
      method: "POST",
      headers: headers(),
      body: JSON.stringify({
        ...draft,
        visibleWhen: draft.visibleWhen || null,
      }),
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      setError((body as { title?: string }).title ?? "Falha ao adicionar pergunta");
      return;
    }
    await openTemplate(selectedId);
  }

  async function removeQuestion(code: string) {
    if (!selectedId) return;
    await fetch(`${API}/v1/studio/briefing-templates/${selectedId}/questions/${code}`, {
      method: "DELETE",
      headers: headers(),
    });
    await openTemplate(selectedId);
  }

  async function publish() {
    if (!selectedId) return;
    const res = await fetch(`${API}/v1/studio/briefing-templates/${selectedId}/publish`, {
      method: "POST",
      headers: headers(),
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      setError((body as { title?: string }).title ?? "Falha ao publicar");
      return;
    }
    await openTemplate(selectedId);
    await loadList();
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-12">
      <h1 className="font-[family-name:var(--font-display)] text-3xl tracking-tight">
        Editor de perguntas
      </h1>
      <p className="mt-2 text-neutral-600">
        Templates versionados do briefing — edite rascunho, publique quando estiver pronto.
      </p>

      {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}

      <div className="mt-8 grid gap-8 md:grid-cols-2">
        <section>
          <h2 className="text-lg font-medium">Templates</h2>
          <ul className="mt-4 divide-y border-t">
            {templates.map((t) => (
              <li key={t.id} className="flex items-center justify-between gap-2 py-3">
                <div>
                  <p className="font-medium">
                    {t.name} · v{t.version}
                  </p>
                  <p className="text-xs text-neutral-500">
                    {t.isPublished ? "Publicado" : "Rascunho"} · {t.questionCount} perguntas
                  </p>
                </div>
                <button
                  type="button"
                  className="border border-neutral-400 px-3 py-1 text-sm"
                  onClick={() => void openTemplate(t.id)}
                >
                  Abrir
                </button>
              </li>
            ))}
          </ul>
        </section>

        <section>
          {selectedId ? (
            <>
              <div className="flex flex-wrap gap-2">
                {published ? (
                  <>
                    <button
                      type="button"
                      className="border border-neutral-400 px-3 py-1.5 text-sm"
                      onClick={() => void openEdit()}
                    >
                      Abrir edicao
                    </button>
                    <button
                      type="button"
                      className="border border-neutral-900 px-3 py-1.5 text-sm"
                      onClick={() => void newVersion()}
                    >
                      Nova versao
                    </button>
                  </>
                ) : (
                  <button
                    type="button"
                    className="border border-neutral-900 bg-neutral-900 px-3 py-1.5 text-sm text-white"
                    onClick={() => void publish()}
                  >
                    Publicar
                  </button>
                )}
              </div>

              <ul className="mt-6 space-y-2 text-sm">
                {questions.map((q) => (
                  <li key={q.code} className="flex items-start justify-between gap-2 border-b py-2">
                    <span>
                      <strong>{q.code}</strong> [{q.block}] {q.prompt}
                      {q.visibleWhen ? (
                        <span className="block text-xs text-neutral-500">when {q.visibleWhen}</span>
                      ) : null}
                    </span>
                    {!published ? (
                      <button
                        type="button"
                        className="text-xs text-red-700"
                        onClick={() => void removeQuestion(q.code)}
                      >
                        Remover
                      </button>
                    ) : null}
                  </li>
                ))}
              </ul>

              {!published ? (
                <div className="mt-6 space-y-3 border border-neutral-200 p-4">
                  <h3 className="font-medium">Nova pergunta</h3>
                  <input
                    className="w-full border px-2 py-1 text-sm"
                    placeholder="Codigo"
                    value={draft.code}
                    onChange={(e) => setDraft({ ...draft, code: e.target.value })}
                  />
                  <input
                    className="w-full border px-2 py-1 text-sm"
                    placeholder="Texto"
                    value={draft.prompt}
                    onChange={(e) => setDraft({ ...draft, prompt: e.target.value })}
                  />
                  <div className="flex gap-2">
                    <select
                      className="border px-2 py-1 text-sm"
                      value={draft.type}
                      onChange={(e) => setDraft({ ...draft, type: e.target.value })}
                    >
                      <option>Text</option>
                      <option>SingleChoice</option>
                      <option>MultiChoice</option>
                      <option>Scale</option>
                      <option>Chips</option>
                      <option>List</option>
                      <option>Url</option>
                      <option>Upload</option>
                    </select>
                    <select
                      className="border px-2 py-1 text-sm"
                      value={draft.block}
                      onChange={(e) => setDraft({ ...draft, block: e.target.value })}
                    >
                      <option>A</option>
                      <option>B</option>
                      <option>C</option>
                      <option>D</option>
                      <option>E</option>
                    </select>
                  </div>
                  <input
                    className="w-full border px-2 py-1 text-sm"
                    placeholder="VisibleWhen (ex: B1.value >= 3)"
                    value={draft.visibleWhen}
                    onChange={(e) => setDraft({ ...draft, visibleWhen: e.target.value })}
                  />
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={draft.isRequired}
                      onChange={(e) => setDraft({ ...draft, isRequired: e.target.checked })}
                    />
                    Obrigatoria
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={draft.isSensitive}
                      onChange={(e) => setDraft({ ...draft, isSensitive: e.target.checked })}
                    />
                    Sensivel
                  </label>
                  <button
                    type="button"
                    className="border border-neutral-900 px-3 py-1.5 text-sm"
                    onClick={() => void addQuestion()}
                  >
                    Adicionar
                  </button>
                </div>
              ) : null}
            </>
          ) : (
            <p className="text-sm text-neutral-500">Selecione um template a esquerda.</p>
          )}
        </section>
      </div>
    </main>
  );
}
