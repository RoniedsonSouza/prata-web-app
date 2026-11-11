> **⚠️ Projeto descontinuado.** Ideia de SaaS para fotógrafos que o autor explorou e abandonou. O código é um protótipo inacabado, não um produto pronto nem de produção. Não há manutenção. Fica público como amostra de arquitetura (Clean Architecture, RLS multi-tenant e o que está em volta).

[![status: descontinuado](https://img.shields.io/badge/status-descontinuado-b31b1b)](#)

# Prata

## O que era

Protótipo de SaaS multi-tenant para fotógrafo autônomo: portfólio público, portal do cliente, back-office e split de pagamento. A peça própria era o briefing de direção de pose. Não chegou a produção.

## Destaques

- Clean Architecture: `Domain` sem dependência externa; o teste de arquitetura quebra o build se a regra falhar.
- Multi-tenancy com `tenant_id` e RLS forçado no PostgreSQL. A API entra como `prata_app`; há teste de isolamento entre tenants.
- Dinheiro em `Money` (`decimal`, `numeric(14,2)`). Sem dado de cartão: só token do PSP.
- Transição de estado na entidade, devolvendo `Result`.
- Dez ADRs: [docs/adr](docs/adr/README.md).

## Stack

| | |
|---|---|
| API | C# 14, .NET 10, Minimal APIs |
| Dados | EF Core 10 (escrita), Dapper (lista), PostgreSQL 17 |
| Front | Next.js 16, React 19, Tailwind CSS v4 |
| Worker | hosted service e outbox. Hangfire está referenciado; o processo que roda é esse worker |
| Pagamento | `IPaymentGateway`: Asaas, ou fake sem chave real |
| Local | `docker compose` sobe Postgres, Redis, MinIO, Mailpit e Seq |

## Como rodar

Só para ler o protótipo. Precisa de .NET 10, Node e Docker.

```bash
docker compose up -d --wait api-deps
psql "postgres://prata_owner:prata_dev_only@localhost:5432/prata" \
  -v app_password=prata_app_dev_only -f scripts/db-roles.sql
dotnet tool restore
dotnet ef database update --project src/Prata.Infrastructure --startup-project src/Prata.Api
dotnet watch --project src/Prata.Api --urls http://localhost:5080
dotnet watch --project src/Prata.Worker
cd web && npm install && npm run dev
```

Front em http://prata.localhost:3000. O `launchSettings` da API usa a porta 5200; o comando acima fixa 5080, que é o padrão do front. O restante está em [docs/14](docs/14-AMBIENTES-E-OPERACAO.md).

## Docs

[Índice](docs/README.md) · [regras](docs/06-REGRAS-DE-NEGOCIO.md) · [multi-tenancy](docs/03-MULTI-TENANCY.md) · [ADRs](docs/adr/README.md)
