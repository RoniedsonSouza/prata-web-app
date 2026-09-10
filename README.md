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

**Só documentação.** Nenhuma linha de aplicação foi escrita — de propósito.

| | |
|---|---|
| ✅ | regras de negócio, arquitetura, decisões, modelo de dados, roadmap |
| ✅ | configuração de build, CI, ambiente local, orçamento de performance |
| ⬜ | código: começa na [E1 · Fundação](docs/etapas/E1-FUNDACAO.md) |

Antes de escrever a primeira linha, leia
[docs/README.md](docs/README.md) e a
[E1](docs/etapas/E1-FUNDACAO.md) — a E1 abre com **três tarefas que não são de
programação** e que travam etapas futuras se ficarem para depois.

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

## Começar

```bash
# dependências (Postgres, Redis, MinIO, Mailpit, Seq)
docker compose up -d --wait api-deps

# papéis do banco — sem isso a API se recusa a subir (RN-TEN-012)
docker compose exec -T postgres psql -U prata_owner -d prata < scripts/db-roles.sql

# configuração
cp .env.example .env      # e preencher os CHANGE_ME

# ferramentas .NET
dotnet tool restore
```

Passo a passo completo, portas e URLs locais:
[docs/14 · Ambientes e operação](docs/14-AMBIENTES-E-OPERACAO.md).

> A porta 5432 pode já estar ocupada por um Postgres local. Todas as portas do
> compose são configuráveis: `POSTGRES_PORT=5433 docker compose up -d`.

## Verificar

```bash
./scripts/check-docs.sh   # link quebrado, RN citada sem definição, ADR inexistente, segredo vazado
```

O mesmo script roda no CI. Quando houver código, o CI acrescenta build,
testes, **gate de isolamento entre tenants**, orçamento de bundle e
Lighthouse.

## Estrutura

```
prata/
├── docs/                 17 documentos de referência + 10 ADRs + 5 etapas
├── scripts/              check-docs.sh · db-roles.sql
├── .github/              CI + orçamento de performance
├── Directory.*.props     build e versões centralizadas
├── docker-compose.yml    ambiente local
└── src/ tests/ web/      ← ainda não existem. Criados na E1
```

## Documentação

Índice completo em **[docs/README.md](docs/README.md)**. Os quatro que mais
importam antes de codar:

| | |
|---|---|
| [00 · Visão e escopo](docs/00-VISAO-E-ESCOPO.md) | o produto, os atores e **o que ele não é** |
| [03 · Multi-tenancy](docs/03-MULTI-TENANCY.md) | o único risco crítico do produto |
| [06 · Regras de negócio](docs/06-REGRAS-DE-NEGOCIO.md) | 123 regras numeradas e testáveis |
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
