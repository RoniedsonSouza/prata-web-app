# Deploy e publicação (sem infra real neste repo)

Checklist operacional. **Nada aqui foi provisionado neste repositório** —
só código + exemplos. Credenciais ficam no cofre do provedor / `.env` local
(nunca commitadas; `.env.example` usa `CHANGE_ME`).

## Front (Vercel)

- Root Directory: `web`
- Wildcard DNS: `*.prata.app` (e apex `prata.app` se houver landing) → projeto Vercel
- Env tipicos: `NEXT_PUBLIC_API_URL`, `REVALIDATE_SECRET`, `SENTRY_DSN`,
  `NEXT_PUBLIC_SENTRY_DSN`
- Resolução de tenant no front: Host `{slug}.prata.app` (ADR-0004)

## API / Worker (container)

```bash
docker build -t prata-api .
docker run --env-file .env -p 8080:8080 prata-api
```

- Migrations: `ConnectionStrings__PrataOwner`
- Runtime: **somente** `ConnectionStrings__PrataApp` (RN-TEN-012)
- Seq opcional: `Seq__ServerUrl` (+ `Seq__ApiKey`)

## DNS / domínio

| Registro | Aponta para | Nota |
|---|---|---|
| `*.prata.app` | front (Vercel) | obrigatório para E1 pública |
| `api.prata.app` | API (Fly / ACA) | TLS no provedor |
| CNAME do fotógrafo | front | domínio próprio (E5); status em `tenant.custom_domain` |

A API já resolve por slug (`{slug}.prata.app` / `/v1/t/{slug}`) e por
`custom_domain` ativo; com domínio próprio ativo, o subdomínio responde
`301` (RN-TEN-013). SSL ACME do domínio do cliente **não** está neste escopo.

## Pagamentos (Asaas)

| Variavel | Uso |
|---|---|
| `Payments__Provider` | `Asaas` para ligar o adapter; sem chaves reais permanece Fake |
| `Payments__Asaas__BaseUrl` | sandbox `…/v3` ou produção |
| `Payments__Asaas__ApiKey` | chave da **plataforma** |
| `Payments__Asaas__WebhookSecret` | header `asaas-access-token` |
| `Payments__Asaas__WalletIdPlatform` | wallet da comissão |

Webhook no painel Asaas: `https://api.prata.app/v1/webhooks/asaas`
(local: túnel → mesma path). Habilitação marketplace/split é **manual** no
Asaas e fora do código.

## Checklist antes do piloto

- [ ] Domínio + wildcard DNS + certificado
- [ ] Conta Asaas + split solicitado / aprovado
- [ ] Secrets no cofre (nada `CHANGE_ME` em produção)
- [ ] Migration aplicada; API sobe como `prata_app`
- [ ] `TenantIsolationTests` verde no CI
- [ ] Webhook sandbox recebendo evento de teste

Ver [14 · Ambientes](14-AMBIENTES-E-OPERACAO.md) e [E1 §3.7](etapas/E1-FUNDACAO.md).
