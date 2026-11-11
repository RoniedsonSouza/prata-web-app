> **⚠️ Project status: Discontinued**
>
> This was a SaaS idea for independent photographers. The author explored it and decided not to pursue it. The code is an unfinished prototype, not a finished or production product. It is no longer maintained. It stays public as a portfolio sample of architecture and engineering practice: Clean Architecture, multi-tenant PostgreSQL row-level security, domain tests, and ADRs.

[![status: discontinued](https://img.shields.io/badge/status-discontinued-b31b1b)](#)
[![archived](https://img.shields.io/badge/archived-9e9e9e)](#)

# Prata

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Next.js](https://img.shields.io/badge/Next.js-16-000000?logo=nextdotjs&logoColor=white)](https://nextjs.org/)
[![CI](https://github.com/RoniedsonSouza/prata-web-app/actions/workflows/ci.yml/badge.svg)](https://github.com/RoniedsonSouza/prata-web-app/actions/workflows/ci.yml)

English · [Português](#português)

> **Unfinished prototype of a multi-tenant SaaS for independent photographers: public portfolio, client portal, studio back-office, and payment split.**

A working photographer usually runs four disconnected tools: Instagram as a portfolio, WhatsApp as a CRM, a spreadsheet as the books, and Drive as delivery. Prata was an attempt to join those four and add the piece none of them have: a **pose-direction briefing**. The bet was that the main reason a session disappoints is not technique — the person did not like themselves in the photo — and that this is predictable if someone asks before the shoot. The briefing was meant to produce a direction sheet the photographer takes to the session.

Scope of the prototype: **v0.1**. **Prata** was a provisional codename. The idea was a Brazilian product: BRL, pt-BR, and a commission on each settled charge via the payment provider, with the platform never holding the money. Stages E1–E5 exist here as prototype code. Nothing was deployed, and the idea was dropped. See [What was built](#what-was-built).

## Highlights for reviewers

- **Clean Architecture is enforced by tests.** `Prata.Domain` has no package references. `Prata.Application` references Domain only. `tests/Prata.Domain.Tests/ArchitectureTests.cs` fails the build if that rule slips. [ADR-0001](docs/adr/ADR-0001-dotnet-10-clean-architecture.md).
- **Tenant leakage is treated as the critical risk.** Every business table has `tenant_id uuid NOT NULL` and implements `ITenantOwned`. Migrations enable and **force** PostgreSQL row-level security. The API connects as `prata_app` (`NOBYPASSRLS`, not the table owner) and refuses to boot on the migration role (`RN-TEN-012`). `TenantIsolationTests` runs against real Postgres via Testcontainers, as `prata_app`, and must fail when one tenant reads another. CI fails if that test disappears.
- **Money and card data have a hard shape.** Amounts are a `Money` value object (`decimal`) and `numeric(14,2)` in the database. `double` and `float` are rejected on that type (`RN-FIN-003`). The API stores a PSP token, never a PAN, CVV, or expiry (`RN-FIN-002`).
- **State lives on the entity.** Transitions are named in the business language and return `Result` — `Order.Confirmar()` — rather than `entity.Status = ...` in a handler. Confirming an order requires a confirmed deposit **and** a signed contract (`RN-COM-020`). State enums are Portuguese (`OrderStatus.Rascunho`); types and methods around them are English ([ADR-0009](docs/adr/ADR-0009-idioma-do-codigo.md)).
- **The dispatcher is ours.** MediatR has been commercially licensed since v13, so commands and queries go through a small `Dispatcher` in `Prata.Application`. Assertions use AwesomeAssertions for the same reason. Both choices are written down, with the alternatives that were refused.
- **Ten ADRs**, each with context, the decision, and the options that lost. Start at [docs/adr](docs/adr/README.md).
- **Sensitive briefing answers stay out of logs, traces, errors, and email** (`RN-BRF-031`, `RN-LGP-005`). A worker purges them on a schedule.

## Key features

| Surface | What it does |
|---|---|
| Public portfolio | Per-tenant site on `{slug}.prata.localhost`. The intended public host was `{slug}.prata.app`; that deploy never happened. Real HTML for SEO; motion and WebGL load after. |
| Client portal | Orders, briefing, galleries. Route group `web/app/(portal)`. |
| Studio | Orders, catalog, briefing templates, finance, audit. Route group `web/app/(studio)`. |
| Platform | Tenant list, suspend, audit read. `platform.admin` cannot read briefings or photos. |
| Commercial | Clients, quotes, orders. The order state machine lives on `Order`. |
| Briefing | Templates, conditional questions, answers, direction sheet (PDF via QuestPDF). |
| Billing | Deposit and balance, split rule, payout account, reconciliation. `IPaymentGateway` is either `FakePaymentGateway` or `AsaasPaymentGateway`. |
| Delivery | Gallery, image derivatives (ImageSharp), share link. Originals stay in a private S3-compatible bucket. |
| Agenda and contract | Availability, blackout dates, booking, in-house electronic acceptance with an audit trail ([ADR-0006](docs/adr/ADR-0006-assinatura-eletronica-propria.md)). |

Tenant resolution uses the host (`{slug}.prata.localhost`). Fallback: header `X-Tenant-Slug` or path `/v1/t/{slug}`.

## Architecture

Modular monolith. One database, one API, one worker, one Next.js app. No microservices.

```
src/Prata.Domain            entities, Money, Result, state transitions — zero dependencies
src/Prata.Application       use cases, ports (IPaymentGateway, ITenantContext), Dispatcher
src/Prata.Infrastructure    EF Core, Dapper, Asaas, S3, ImageSharp, JWT, RLS interceptor
src/Prata.Api               ASP.NET Core Minimal APIs under /v1
src/Prata.Worker            hosted service: outbox and daily processors
tests/                      domain, application, API integration (Testcontainers)
web/                        Next.js App Router — (public), (portal), (studio)
```

| Layer | What the repository actually uses |
|---|---|
| API and domain | C# 14 (`LangVersion` 14) · .NET 10 (`net10.0`) · ASP.NET Core Minimal APIs · Clean Architecture |
| Persistence | EF Core 10 for aggregates and writes · Dapper for back-office lists, with `TenantId` as an explicit parameter · snake_case columns |
| Database | PostgreSQL 17 · `ENABLE` + `FORCE ROW LEVEL SECURITY` · separate owner and app roles |
| Front | TypeScript · Next.js 16.3 · React 19 · Tailwind CSS v4 · GSAP + Lenis on the public site · Sentry |
| WebGL | A small WebGL2 canvas for immersive themes (`web/components/immersive-webgl.ts`). `three` is not a dependency; portal and studio stay free of it ([ADR-0010](docs/adr/ADR-0010-motion-e-webgl.md)). `prefers-reduced-motion` turns motion off. |
| Jobs | `Prata.Worker` drains a transactional outbox and runs daily processors: quote expiry, payment reconciliation, gallery expiry, reminders, sensitive-briefing purge. Hangfire.Core 1.8 and Hangfire.PostgreSql 1.21 are referenced, and `.env.example` reserves a `hangfire` schema connection string, for the Postgres-backed scheduler described in the architecture notes. |
| Storage | AWSSDK.S3. MinIO in `docker-compose`. The intended production store was Cloudflare R2 ([ADR-0008](docs/adr/ADR-0008-storage-r2-derivadas-no-worker.md)). |
| Payments | Asaas behind `IPaymentGateway` ([ADR-0005](docs/adr/ADR-0005-psp-asaas-com-port.md)). The fake gateway is used unless `Payments:Provider=Asaas` and both the API key and the webhook secret are set to something other than `CHANGE_ME`. Webhook: `POST /v1/webhooks/asaas`. |
| Auth | ASP.NET Core Identity + JWT (`tenant_id`, `role`). Access token 15 minutes, rotating refresh-token family. |
| Observability | Serilog · OpenTelemetry (ASP.NET Core, HttpClient, Npgsql) · Seq in compose · Sentry on the front. Log filters drop tokens, passwords, and connection strings. |
| Local dependencies | `docker-compose.yml` starts PostgreSQL 17, Redis 8, MinIO, Mailpit, and Seq. It does not start the API, the worker, or Next.js. The API rate limiter in this tree is the in-process ASP.NET Core fixed window. |

Business-rule IDs (`RN-…`) are the join key between the catalog, the code, and the test names. The catalog is [docs/06-REGRAS-DE-NEGOCIO.md](docs/06-REGRAS-DE-NEGOCIO.md).

## How to run

These steps are how the prototype was started on a machine. They are a way to read the sample, not support for a product.

Requirements: .NET 10 SDK, Node.js 24 (the version the workflow file uses), Docker.

`docker compose` only starts dependencies. The API reads `src/Prata.Api/appsettings.json` in Development (local connection strings are already there). ASP.NET does not load the root `.env` by itself. Copy `.env.example` to `.env` when you want the full variable set; replace every `CHANGE_ME`. The JWT signing key must be at least 32 characters.

```bash
# Postgres, Redis, MinIO, Mailpit, Seq
docker compose up -d --wait api-deps

# App role. Pass the password explicitly: scripts/db-roles.sql otherwise
# falls back to a different development password than appsettings.json.
# Run again after migrations so new tables are granted and audit_log
# loses UPDATE/DELETE for prata_app.
psql "postgres://prata_owner:prata_dev_only@localhost:5432/prata" \
  -v app_password=prata_app_dev_only \
  -f scripts/db-roles.sql

dotnet tool restore
dotnet ef database update \
  --project src/Prata.Infrastructure \
  --startup-project src/Prata.Api

# launchSettings.json binds the API to port 5200.
# The Next.js app defaults to http://localhost:5080, so pin the URL.
dotnet watch --project src/Prata.Api --urls http://localhost:5080
dotnet watch --project src/Prata.Worker

cd web && npm install && npm run dev
```

Port 5432 may already be taken. Every compose port is overridable: `POSTGRES_PORT=5433 docker compose up -d`. If you change it, point the connection strings at the same port.

`*.localhost` resolves to `127.0.0.1` in current Chrome and Firefox. If it does not:

```
127.0.0.1  prata.localhost estudio-demo.prata.localhost
```

Without real keys the API uses `FakePaymentGateway`. The notes for a real Asaas account are in [docs/ops/ASAAS-SETUP.md](docs/ops/ASAAS-SETUP.md). No production account was connected.

### Local map

| URL | What |
|---|---|
| http://prata.localhost:3000 | Front apex (`prata` is a reserved slug) |
| http://{slug}.prata.localhost:3000 | Public portfolio (example slug `estudio-demo`) |
| …/portal | Client portal: orders, briefing, galleries |
| …/studio | Studio: orders, finance, briefing, audit |
| http://localhost:5080 | API `/v1/…` · Scalar UI at `/scalar` in Development |
| http://localhost:5080/health · `/health/ready` | Liveness / readiness |
| http://localhost:5080/v1/webhooks/asaas | Signed PSP webhook |
| http://localhost:8025 | Mailpit |
| http://localhost:5341 | Seq |
| http://localhost:9000 | MinIO (S3) |

Full ops notes, deploy order, and job list: [docs/14 · Environments and operations](docs/14-AMBIENTES-E-OPERACAO.md). API catalog: [docs/10](docs/10-API.md). Front performance budget: [docs/16](docs/16-FRONTEND-E-EXPERIENCIA.md).

### Verify

```bash
./scripts/check-docs.sh
dotnet csharpier check .
dotnet build          # warnings are errors
dotnet test           # integration tests need Docker (Testcontainers)
cd web && npm run typecheck && npm run size
```

The workflow in `.github/workflows/ci.yml` was written to run the doc check, CSharpier, build, unit tests, Testcontainers integration tests, a gate that `TenantIsolationTests` still exists, the Next.js typecheck/lint/build, the bundle budget, and Lighthouse on the public portfolio. It is a snapshot of how the prototype was checked. It is not an offer of ongoing maintenance.

## Layout

```
prata/
├── docs/                 reference, ADRs, stage notes
├── scripts/              check-docs.sh · db-roles.sql
├── .github/              CI and Lighthouse budget
├── Directory.*.props     shared build settings and package versions
├── docker-compose.yml    local dependencies
├── src/                  Api · Application · Domain · Infrastructure · Worker
├── tests/                Domain · Application · Api.IntegrationTests
└── web/                  Next.js (public, portal, studio)
```

Package versions are declared only in `Directory.Packages.props`.

## Documentation

Index: **[docs/README.md](docs/README.md)**. The four that matter first:

| | |
|---|---|
| [00 · Vision and scope](docs/00-VISAO-E-ESCOPO.md) | the product, the actors, and what it refuses to be |
| [02 · Architecture](docs/02-ARQUITETURA.md) | dependency rule, ports, why this stack |
| [03 · Multi-tenancy](docs/03-MULTI-TENANCY.md) | isolation, RLS, the leak checklist |
| [06 · Business rules](docs/06-REGRAS-DE-NEGOCIO.md) | numbered, testable rules |
| [13 · Roadmap and risks](docs/13-ROADMAP-E-RISCOS.md) | the stage plan and risks, as written at the time |
| [adr](docs/adr/README.md) | ten accepted decisions |

[CLAUDE.md](CLAUDE.md) is the construction checklist followed while this prototype was written.

## What was built

Historical record. The author stopped. There is no next stage, no pilot, and no production deploy.

| | |
|---|---|
| In this repository | Business rules, architecture, ADRs, data model, and the roadmap as it was written |
| In this repository | `src/`, `tests/`, `web/` — prototype API, domain, worker, and front for the old stages E1–E5 |
| In this repository | `FakePaymentGateway` in dev and tests; `AsaasPaymentGateway` only if keys were configured |
| Never done | Real deploy (`*.prata.app`), Asaas marketplace enablement, pilot photographer |

The acceptance notes in [13 · Roadmap](docs/13-ROADMAP-E-RISCOS.md) describe what would have counted as production. They were not met, and the work stopped.

The stage plan, kept as history:

| Stage | What that stage was for |
|---|---|
| [E1](docs/etapas/E1-FUNDACAO.md) | Tenant, auth, RLS, catalog, public portfolio |
| [E2](docs/etapas/E2-COMERCIAL.md) | Client, order, briefing, direction sheet |
| [E3](docs/etapas/E3-DINHEIRO.md) | PSP, KYC, split, deposit and balance, reconciliation |
| [E4](docs/etapas/E4-ENTREGA.md) | Upload, derivatives, gallery, selection, ZIP |
| [E5](docs/etapas/E5-AGENDA-E-CONTRATO.md) | Agenda, signed contract, reminders |

Two product questions were expected to move after conversations with working photographers: the order states ([05](docs/05-MAQUINAS-DE-ESTADO.md)) and the revenue model ([ADR-0003](docs/adr/ADR-0003-modelo-de-receita-comissao.md)). Those conversations did not continue.

---

## Português

> **⚠️ Projeto descontinuado.** Ideia de SaaS para fotógrafos que o autor explorou e decidiu não levar adiante. O código é um protótipo inacabado, não um produto pronto nem de produção. Não há manutenção. O repositório fica público como amostra de portfólio: Clean Architecture, RLS multi-tenant e o restante das práticas de engenharia.

# Prata

> **SaaS vertical multi-tenant de gestão e entrega fotográfica, com portfólio
> público, portal do cliente e split de pagamento.**

Escopo **v0.1** · setembro de 2026 · **codinome provisório da época**.

Um fotógrafo autônomo opera hoje com quatro ferramentas desconectadas:
Instagram como portfólio, WhatsApp como CRM, planilha como financeiro e Drive
como entrega. O Prata junta as quatro — e acrescenta a peça que ninguém tem:
**briefing de direção de pose**, porque a principal causa de insatisfação com
um ensaio não é técnica, é a pessoa não ter gostado de si mesma na foto.

---

## Estado atual

**Projeto descontinuado.** Não há piloto aberto, próximo passo nem deploy. O que segue é o registro do protótipo.

| | |
|---|---|
| No repositório | regras de negócio, arquitetura, ADRs, modelo de dados, roadmap da época |
| No repositório | `src/`, `tests/`, `web/` — API, domínio, worker e front do protótipo |
| No repositório | FakePaymentGateway em dev/testes; `AsaasPaymentGateway` se houvesse chaves |
| Não feito | Deploy real (`*.prata.app`), habilitação marketplace Asaas, fotógrafo piloto |

Os documentos de construção permanecem como histórico:
[docs/README.md](docs/README.md) e [docs/etapas/](docs/etapas/). Os critérios
que teriam valido para produção estão em
[13 · Roadmap](docs/13-ROADMAP-E-RISCOS.md). Não foram atingidos.

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

Como o protótipo subia na máquina. Não é guia de produto suportado.

**Na época do 1º start:** copie `.env.example` → `.env`, suba as deps, papéis do
banco (`db-roles.sql`) e migrations — tudo em
[docs/14 · Ambientes e operação](docs/14-AMBIENTES-E-OPERACAO.md).

Sem chaves reais a API usa `FakePaymentGateway`. O passo a passo de uma conta
Asaas ficou em [docs/ops/ASAAS-SETUP.md](docs/ops/ASAAS-SETUP.md). Nenhuma conta
de produção foi ligada.

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
| [13 · Roadmap e riscos](docs/13-ROADMAP-E-RISCOS.md) | plano E1–E5 e riscos, como estavam escritos |

E [CLAUDE.md](CLAUDE.md) — as regras de construção usadas enquanto o protótipo era escrito.

## Roadmap (histórico)

Plano da época em que a ideia ainda estava em exploração. Não é próximo passo: o projeto foi descontinuado antes de virar produto.

| Etapa | Entrega prevista | Marco previsto |
|---|---|---|
| [E1](docs/etapas/E1-FUNDACAO.md) | tenant, auth, RLS, catálogo, portfólio público | um fotógrafo real usa como site dele |
| [E2](docs/etapas/E2-COMERCIAL.md) | cliente, pedido, briefing, ficha de direção | substitui o WhatsApp na entrada de lead |
| [E3](docs/etapas/E3-DINHEIRO.md) | PSP, KYC, split, sinal e saldo, conciliação | a plataforma passaria a faturar |
| [E4](docs/etapas/E4-ENTREGA.md) | upload, derivadas, galeria, seleção, ZIP | fecharia o ciclo e ligaria a indicação |
| [E5](docs/etapas/E5-AGENDA-E-CONTRATO.md) | agenda, contrato assinado, lembretes | produto completo |

O prazo imaginado era de **5 a 7 meses**, um dev meio período. E3 vinha antes de E4 de propósito: galeria bonita sem cobrança é um custo; cobrança sem galeria já é um negócio. Esse plano não foi até um produto em produção.

## Este escopo era para ser cortado

Texto da época em que o escopo ainda ia ser discutido com fotógrafos. Essa conversa não seguiu.

Feito para ser discutido, não seguido à risca. Os dois pontos que mais mudariam
depois de conversar com dois ou três fotógrafos reais eram **os estados do
pedido** ([05](docs/05-MAQUINAS-DE-ESTADO.md)) e **o modelo de receita**
([ADR-0003](docs/adr/ADR-0003-modelo-de-receita-comissao.md)). As perguntas que
seriam feitas a eles estão em [13, seção 6](docs/13-ROADMAP-E-RISCOS.md).
