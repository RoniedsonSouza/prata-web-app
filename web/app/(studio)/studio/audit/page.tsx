"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type AuditRow = {
  id: string;
  tenantId?: string;
  action: string;
  entityType: string;
  at: string;
};

const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

/** Console E1: login platform.admin + lista AuditLog. */
export default function StudioAuditPage() {
  const [email, setEmail] = useState("admin@prata.app");
  const [password, setPassword] = useState("");
  const [token, setToken] = useState<string | null>(null);
  const [logs, setLogs] = useState<AuditRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function login() {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${apiBase}/v1/platform/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
      if (!res.ok) {
        setError("Falha no login (platform.admin).");
        return;
      }
      const data = (await res.json()) as { accessToken: string };
      setToken(data.accessToken);
      await loadLogs(data.accessToken);
    } catch {
      setError("API indisponivel.");
    } finally {
      setLoading(false);
    }
  }

  async function loadLogs(accessToken: string) {
    const res = await fetch(`${apiBase}/v1/platform/audit-logs`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    if (!res.ok) {
      setError("Sem permissao para AuditLog.");
      return;
    }
    setLogs((await res.json()) as AuditRow[]);
  }

  return (
    <main className="mx-auto max-w-3xl px-6 py-16">
      <h1 className="text-2xl font-medium">Console · Audit log</h1>
      <p className="mt-2 text-sm text-neutral-600">
        Autenticacao <code>platform.admin</code> de ponta a ponta (E1 §3.8).
      </p>

      {!token ? (
        <form
          className="mt-8 flex max-w-md flex-col gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            void login();
          }}
        >
          <Input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="e-mail"
            required
          />
          <Input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="senha"
            required
            minLength={10}
          />
          <Button type="submit" disabled={loading}>
            {loading ? "Entrando…" : "Entrar"}
          </Button>
        </form>
      ) : (
        <div className="mt-8">
          <div className="mb-4 flex gap-2">
            <Button type="button" variant="ghost" onClick={() => void loadLogs(token)}>
              Atualizar
            </Button>
            <Button type="button" variant="ghost" onClick={() => setToken(null)}>
              Sair
            </Button>
          </div>
          <ul className="divide-y divide-stone-200 border border-stone-200">
            {logs.length === 0 ? (
              <li className="px-4 py-6 text-sm text-stone-500">Nenhum evento ainda.</li>
            ) : (
              logs.map((row) => (
                <li key={row.id} className="px-4 py-3 text-sm">
                  <span className="font-medium">{row.action}</span>
                  <span className="text-stone-500"> · {row.entityType}</span>
                  <span className="block text-xs text-stone-400">{row.at}</span>
                </li>
              ))
            )}
          </ul>
        </div>
      )}

      {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}
    </main>
  );
}
