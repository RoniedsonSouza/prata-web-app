# Prata

> **SaaS vertical multi-tenant de gestão e entrega fotográfica, com portfólio
> público, portal do cliente e split de pagamento.**

Escopo **v0.1** · setembro de 2026 · **codinome provisório, trocável**.

Um fotógrafo autônomo opera hoje com quatro ferramentas desconectadas:
Instagram como portfólio, WhatsApp como CRM, planilha como financeiro e Drive
como entrega. O Prata junta as quatro — e acrescenta a peça que ninguém tem:
**briefing de direção de pose**, porque a principal causa de insatisfação com
um ensaio não é técnica, é a pessoa não ter gostado de si mesma na foto.

---

## Estado atual

**Código E1–E5 na branch `feat/e5-agenda`.** Aceite de piloto/produção
**ainda aberto** (DNS wildcard, conta Asaas marketplace, fotógrafo piloto).

| | |
|---|---|
| ✅ | regras de negócio, arquitetura, ADRs, modelo de dados, roadmap |
| ✅ | `src/`, `tests/`, `web/` — API, domínio, workers e front na branch |
| ✅ | FakePaymentGateway em dev/testes; `AsaasPaymentGateway` quando configurado |
| ⬜ | Deploy real (`*.prata.app`), habilitação marketplace Asaas, aceite piloto |

Antes de contribuir, leia [docs/README.md](docs/README.md) e a etapa em
[docs/etapas/](docs/etapas/). Critérios de aceite de produção continuam em
[13 · Roadmap](docs/13-ROADMAP-E-RISCOS.md) — **não** marcar como feitos só
porque o código existe.

## Stack

| Camada | Tecnologia |
|---|---|
| API e domínio | C# 14 · .NET 10 LTS · ASP.NET Core Minimal APIs · Clean Architecture |
| Persistência | EF Core 10 (escrita) · Dapper (listagem) · PostgreSQL 17 com RLS |
| Front | TypeScript · Next.js 16 · Tailwind v4 · GSAP + Lenis · R3F (tema premium) |
| Jobs | Hangfire sobre Postgres |
| Storage | S3-compatible: Cloudflare R2 (MinIO em dev) |
| Pagamento | Asaas, com split e subcontas, atrás de `IPaymentGateway` |
| Observabilidade | OpenTelemetry + Serilog → Seq · Sentry no front |

Decisões e alternativas recusadas: [docs/adr](docs/adr/README.md).

## Dev local (rápido)

**Antes do 1º start:** copie `.env.example` → `.env`, suba as deps, papéis do
banco (`db-roles.sql`) e migrations — tudo em
[docs/14 · Ambientes e operação](docs/14-AMBIENTES-E-OPERACAO.md).

Asaas **não** é obrigatório no dia 1: sem chaves reais a API usa
`FakePaymentGateway`. PSP real: [docs/ops/ASAAS-SETUP.md](docs/ops/ASAAS-SETUP.md).

```bash
# deps (Postgres, Redis, MinIO, Mailpit, Seq)
docker compose up -d --wait api-deps

# API + Worker (terminais separados)
dotnet watch --project src/Prata.Api
dotnet watch --project src/Prata.Worker

# Front
cd web && npm install && npm run dev
```

### Mapa local (dev)

**Arquitetura:** o Host (`{slug}.prata.localhost` / `.prata.app`) resolve o
tenant no front e na API (fallback: header `X-Tenant-Slug` ou path
`/v1/t/{slug}`); front e API são origins distintos (`:3000` vs `:5080`);
webhook do Asaas chega na API (`/v1/webhooks/asaas`), em local via túnel.

| URL | O quê | Quem / papel |
|---|---|---|
| http://prata.localhost:3000 | Apex do front (`prata` é slug reservado) | Dev / entrada da plataforma |
| http://{slug}.prata.localhost:3000 | Site público do fotógrafo (ex.: `estudio-demo`) | Visitante · SEO |
| …/portal · …/portal/orders/… · …/portal/galleries/… | Portal do cliente (pedido, briefing, galeria) | Cliente do tenant |
| …/studio · …/studio/orders · …/studio/briefing · …/studio/finance | Back-office do estúdio | Fotógrafo / staff |
| http://localhost:5080 | API `/v1/…` · OpenAPI em `/scalar` | Front e integrações |
| http://localhost:5080/health · `/health/ready` | Liveness / readiness | Ops · compose |
| http://localhost:5080/v1/t/{slug}/… | Portfólio, tema, serviços (público) | Anônimo |
| http://localhost:5080/v1/auth/… | Login, refresh, registro | Cliente e estúdio |
| http://localhost:5080/v1/portal/… | Pedidos, briefing, galerias | Cliente autenticado |
| http://localhost:5080/v1/studio/… | Pedidos, catálogo, finanças, agenda, galerias | Estúdio autenticado |
| http://localhost:5080/v1/platform/… | Console da plataforma | `platform.admin` |
| http://localhost:5080/v1/webhooks/asaas | Eventos do PSP (assinados) | Asaas |

No Next, três grupos em `web/app`: `(public)`, `(portal)`, `(studio)` —
detalhe de render/motion em [docs/16](docs/16-FRONTEND-E-EXPERIENCIA.md).
Catálogo fino da API: [docs/10](docs/10-API.md). Portas extras (Mailpit, Seq,
MinIO) e wildcard `*.localhost`: [docs/14](docs/14-AMBIENTES-E-OPERACAO.md).
Publicação / DNS: [docs/deploy-e1.md](docs/deploy-e1.md).

> A porta 5432 pode já estar ocupada por um Postgres local. Todas as portas do
> compose são configuráveis: `POSTGRES_PORT=5433 docker compose up -d`.

## Verificar

```bash
./scripts/check-docs.sh
dotnet csharpier check .
dotnet build
dotnet test
cd web && npm run typecheck && npm run size
```

## Estrutura

```
prata/
├── docs/                 referência + ADRs + etapas
├── scripts/              check-docs.sh · db-roles.sql
├── .github/              CI + orçamento de performance
├── Directory.*.props     build e versões centralizadas
├── docker-compose.yml    ambiente local
├── src/                  Api · Application · Domain · Infrastructure · Worker
├── tests/                Domain · Application · Api.IntegrationTests
└── web/                  Next.js (público, portal, studio)
```

## Documentação

Índice completo em **[docs/README.md](docs/README.md)**. Os quatro que mais
importam:

| | |
|---|---|
| [00 · Visão e escopo](docs/00-VISAO-E-ESCOPO.md) | o produto, os atores e **o que ele não é** |
| [03 · Multi-tenancy](docs/03-MULTI-TENANCY.md) | o único risco crítico do produto |
| [06 · Regras de negócio](docs/06-REGRAS-DE-NEGOCIO.md) | regras numeradas e testáveis |
| [13 · Roadmap e riscos](docs/13-ROADMAP-E-RISCOS.md) | E1–E5, critérios de aceite, decisões abertas |

E [CLAUDE.md](CLAUDE.md) — as regras de construção que valem em todo commit.

## Roadmap

| Etapa | Entrega | Marco |
|---|---|---|
| [E1](docs/etapas/E1-FUNDACAO.md) | tenant, auth, RLS, catálogo, portfólio público | um fotógrafo real usa como site dele |
| [E2](docs/etapas/E2-COMERCIAL.md) | cliente, pedido, briefing, ficha de direção | substitui o WhatsApp na entrada de lead |
| [E3](docs/etapas/E3-DINHEIRO.md) | PSP, KYC, split, sinal e saldo, conciliação | **a plataforma passa a faturar** |
| [E4](docs/etapas/E4-ENTREGA.md) | upload, derivadas, galeria, seleção, ZIP | fecha o ciclo e liga a indicação |
| [E5](docs/etapas/E5-AGENDA-E-CONTRATO.md) | agenda, contrato assinado, lembretes | produto completo |

**5 a 7 meses** para as cinco, um dev meio período. E3 vem antes de E4 de
propósito: galeria bonita sem cobrança é um custo; cobrança sem galeria já é
um negócio.

## Este escopo é para ser cortado

Feito para ser discutido, não seguido à risca. Os dois pontos que mais mudam
depois de conversar com dois ou três fotógrafos reais são **os estados do
pedido** ([05](docs/05-MAQUINAS-DE-ESTADO.md)) e **o modelo de receita**
([ADR-0003](docs/adr/ADR-0003-modelo-de-receita-comissao.md)). As perguntas a
fazer a eles estão em [13, seção 6](docs/13-ROADMAP-E-RISCOS.md).
