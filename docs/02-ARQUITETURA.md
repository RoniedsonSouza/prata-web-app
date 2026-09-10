# 02 · Arquitetura

Monólito modular em Clean Architecture. Um banco, uma API, um worker, um app
de front. Nada de microsserviço: com um desenvolvedor meio período,
microsserviço não é arquitetura, é dívida operacional.

---

## 1. Stack

| Camada | Tecnologia | Por quê |
|---|---|---|
| API e domínio | **C# 14 / .NET 10 LTS**, ASP.NET Core Minimal APIs | LTS até nov/2028. Domínio de dinheiro e máquina de estados pede tipagem forte e teste de unidade barato |
| Persistência | **EF Core 10** para escrita e agregados; **Dapper** para listagem | EF garante invariante de agregado. Dapper onde a listagem do back-office precisa de SQL cru sem materializar grafo |
| Banco | **PostgreSQL 17** (`jsonb`, RLS) | RLS é a segunda barreira de isolamento. `jsonb` guarda resposta de briefing sem virar EAV |
| Front | **TypeScript / Next.js 16** App Router | Ver a correção na seção 7 e em [16](16-FRONTEND-E-EXPERIENCIA.md) |
| Estilo | **Tailwind v4** + shadcn/ui, tokens por tenant em CSS custom properties | Tema resolvido em runtime, não em build |
| Imagens | **ImageSharp** no worker | Derivada fora do request. `imgproxy` em container se o volume crescer |
| Jobs | **Hangfire** sobre Postgres | Já temos Postgres. Dashboard e retry prontos. Não precisa de broker |
| Storage | **S3-compatible**: Cloudflare R2 (MinIO em dev) | R2 não cobra egress — decisivo em produto que serve gigabytes de foto |
| Cache / fila leve | **Redis** | Sessão de galeria, rate limit, idempotência de webhook |
| Auth | ASP.NET Core Identity + JWT, claims `tenant_id` e `role` | Keycloak só se aparecer SSO corporativo. Não é o caso |
| Observabilidade | OpenTelemetry + Serilog → Seq/Grafana; Sentry no front | Falha de webhook de pagamento tem que ser visível em minutos |
| Infra | Docker, GitHub Actions | API em Fly.io / Azure Container Apps; front na Vercel; DB em Neon ou RDS |

### Por que não tudo em TypeScript

Seria defensável, e para muitos devs solo seria o conselho. Aqui não é: a
entrega em C# é várias vezes mais rápida para este desenvolvedor, e o núcleo
— estados de pedido, split, conciliação — é exatamente o domínio em que Clean
Architecture com C# paga o próprio custo. O front fica em TS porque não há
alternativa razoável para SEO de portfólio.

## 2. Estrutura da solução

```
prata/
├── src/
│   ├── Prata.Domain                 — sem dependência externa. Nenhuma.
│   │   ├── Common/                  Entity, AggregateRoot, ValueObject, DomainEvent, Result, Money
│   │   ├── Tenancy/                 Tenant, TenantSettings, User, Role, Invite
│   │   ├── Catalog/                 ServiceType, Package, Addon, PriceTable
│   │   ├── Showcase/                Collection, CollectionItem, PageContent, SeoMeta
│   │   ├── Sales/                   Client, Order, OrderItem, Quote, Discount, OrderStatus
│   │   ├── Briefing/                BriefingTemplate, Question, Option, Answer, ShootSheet
│   │   ├── Scheduling/              Booking, Availability, BlackoutDate
│   │   ├── Billing/                 Payment, Installment, SplitRule, PayoutAccount, Payout, PaymentEvent
│   │   ├── Delivery/                Gallery, Photo, PhotoVariant, Selection, ShareLink, DownloadJob
│   │   └── Shared/                  Contract, Signature, Notification, AuditLog
│   │
│   ├── Prata.Application            — casos de uso e ports
│   │   ├── Abstractions/            IPaymentGateway, IStorage, IImageProcessor, INotifier,
│   │   │                            ISignatureProvider, IPdfRenderer, ITenantContext,
│   │   │                            IUnitOfWork, IDateTimeProvider, IEventPublisher
│   │   ├── Tenancy/  Catalog/  Showcase/
│   │   ├── Sales/       Commands/ Queries/ Validators/
│   │   ├── Briefing/  Scheduling/  Billing/  Delivery/
│   │   └── Behaviors/               Validation, Logging, Transaction, TenantGuard, Idempotency
│   │
│   ├── Prata.Infrastructure
│   │   ├── Persistence/             PrataDbContext, configurations, migrations, query filters, RLS
│   │   ├── Persistence/Queries/     Dapper — listagens do back-office
│   │   ├── Payments/Asaas/          adapter do PSP + parser e verificador de webhook
│   │   ├── Storage/S3/              presigned URL, multipart
│   │   ├── Imaging/                 ImageSharp — thumb, web, textura, LQIP, marca d'água
│   │   ├── Documents/               QuestPDF — ficha de direção, contrato
│   │   ├── Notifications/           e-mail (SMTP), WhatsApp (wa.me na v1)
│   │   ├── Signature/               aceite próprio: hash, IP, user-agent, timestamp
│   │   └── Identity/                ASP.NET Core Identity, emissão de JWT
│   │
│   ├── Prata.Api                    — Minimal APIs por módulo
│   │   ├── Endpoints/               um arquivo por contexto
│   │   ├── Middleware/              TenantResolution, ExceptionHandling, RateLimit, RequestLogging
│   │   └── Webhooks/                endpoint do PSP: assinado e idempotente
│   │
│   └── Prata.Worker                 — jobs: derivadas, ZIP, cobrança, expiração, reconciliação
│
├── tests/
│   ├── Prata.Domain.Tests           máquinas de estado, dinheiro, invariantes, arquitetura
│   ├── Prata.Application.Tests      casos de uso com ports falsos
│   └── Prata.Api.IntegrationTests   Testcontainers: Postgres real, RLS, isolamento de tenant
│
├── web/                             Next.js 16 — (public) · (portal) · (studio)
└── docs/                            este conjunto
```

## 3. Regra de dependência

```
        Api ──┐                 ┌── Worker
              ▼                 ▼
          Infrastructure ───────┘
                 │
                 ▼
            Application
                 │
                 ▼
              Domain          ← não referencia NADA
```

| Projeto | Pode referenciar |
|---|---|
| `Prata.Domain` | nada. Nem EF, nem `System.Text.Json`, nem pacote de log |
| `Prata.Application` | só `Prata.Domain` |
| `Prata.Infrastructure` | `Application` + `Domain` |
| `Prata.Api`, `Prata.Worker` | `Application` + `Infrastructure` (para compor DI) |

**A intenção não basta.** `tests/Prata.Domain.Tests/ArchitectureTests.cs`
falha o build se alguém referenciar para fora. Ver
[15 · Estratégia de testes](15-ESTRATEGIA-DE-TESTES.md).

Consequência prática: o PSP entra por `IPaymentGateway`. **Trocar Asaas por
Pagar.me deve custar uma classe** em `Infrastructure/Payments/`, sem tocar em
`Domain` nem em `Application`.

## 4. Padrões do domínio

### 4.1 `Result` em vez de exceção para regra de negócio

Exceção é para o que não deveria acontecer (banco fora, disco cheio). Regra de
negócio violada é resultado esperado e vai como valor.

```csharp
// Domain/Common/Result.cs — contrato, não implementação final
public readonly record struct Error(string Code, string Message);

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }
}
```

O `Code` do erro é o que a API traduz para `type` do ProblemDetails e o front
traduz para mensagem. Códigos catalogados em [10 · API](10-API.md).

### 4.2 Transição de estado dentro da entidade

**Nunca** `order.Status = OrderStatus.Confirmado` num handler. A entidade é
quem sabe se a transição é válida, e é ela quem publica o evento.

```csharp
public Result<Unit> Confirmar(bool sinalConfirmado, bool contratoAssinado, DateTimeOffset agora)
{
    if (Status is not OrderStatus.Aprovado)
        return OrderErrors.TransicaoInvalida(Status, OrderStatus.Confirmado);
    if (!sinalConfirmado)   return OrderErrors.SinalNaoConfirmado;     // RN-COM-020
    if (!contratoAssinado)  return OrderErrors.ContratoNaoAssinado;    // RN-COM-020

    Status = OrderStatus.Confirmado;
    ConfirmedAt = agora;
    Raise(new PedidoConfirmado(Id, TenantId, DataPretendida));         // reserva a data
    return Unit.Value;
}
```

Cada transição publica um evento de domínio. A tabela completa de transições,
guardas e eventos está em [05 · Máquinas de estado](05-MAQUINAS-DE-ESTADO.md).

### 4.3 `Money` como Value Object

Dinheiro nunca é `decimal` solto e nunca é `double`.

```csharp
public readonly record struct Money(decimal Amount, Currency Currency)
{
    // Arredondamento bancário explícito, 2 casas, BRL.
    // Nada de double: 0.1 + 0.2 em ponto flutuante é bug de conciliação.
}
```

No banco: `numeric(14,2)`. Ver [11 · Modelo de dados](11-MODELO-DE-DADOS.md).

## 5. Aplicação

### 5.1 Dispatcher próprio, sem MediatR

MediatR passou a exigir licença comercial paga a partir da v13. Clean
Architecture não depende dele: o que se usa é um dispatcher de ~40 linhas
sobre o container de DI. Ver
[ADR-0001](adr/ADR-0001-dotnet-10-clean-architecture.md).

```csharp
public interface ICommand<TResponse>;
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}

public interface IDispatcher
{
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct);
    Task<Result<TResponse>> Ask<TResponse>(IQuery<TResponse> query, CancellationToken ct);
}
```

### 5.2 Pipeline de behaviors

Ordem importa. É a ordem em que se registra no DI:

| # | Behavior | O que faz |
|---|---|---|
| 1 | `RequestLogging` | correlation id, `tenant_id`, duração. **Nunca** loga payload de briefing sensível ([RN-LGP-005](06-REGRAS-DE-NEGOCIO.md)) |
| 2 | `TenantGuard` | rejeita comando sem `ITenantContext.TenantId` resolvido. Ver [03](03-MULTI-TENANCY.md) |
| 3 | `Idempotency` | comando com `Idempotency-Key` já processado retorna o resultado anterior |
| 4 | `Validation` | FluentValidation. Falha vira `400` com lista de campos |
| 5 | `Transaction` | abre transação, aplica, publica eventos pelo outbox, comita |

Query **não** passa por `Transaction` nem `Idempotency`.

### 5.3 Eventos de domínio e outbox

Evento de domínio é levantado pela entidade e coletado no `SaveChanges`. Ele
**não** é publicado direto: vai para a tabela `outbox_message` na mesma
transação do agregado, e o worker entrega depois.

Motivo concreto: `PedidoConfirmado` dispara e-mail e reserva de data. Se o
e-mail for enviado dentro da transação e o commit falhar, o cliente recebe
confirmação de um pedido que não existe. Com outbox, ou os dois acontecem ou
nenhum.

## 6. Persistência: EF ou Dapper

| Use | Onde | Por quê |
|---|---|---|
| **EF Core** | toda escrita, todo carregamento de agregado | Change tracking garante invariante; query filter global é a barreira 1 do tenant |
| **Dapper** | listagem paginada do back-office, painel financeiro, relatório | SQL cru sem materializar grafo. Ganho real de latência na lista de pedidos |

**Regra dura:** consulta em Dapper **não** tem query filter do EF. Toda query
Dapper recebe `TenantId` como parâmetro explícito, e a RLS existe justamente
para o dia em que alguém esquecer. Ver
[03 · Multi-tenancy](03-MULTI-TENANCY.md), seção "As três barreiras".

## 7. Front-end

**Correção da spec original:** ela dizia Next.js 15. A linha atual é a **16**
(`16.3.4` em set/2026); o 15 está em manutenção de backport. Para um produto
que se mantém por anos, começar uma versão maior atrás é dívida de graça —
mesma lógica que levou o backend de .NET 9 para 10 LTS. Ver
[16 · Front-end e experiência](16-FRONTEND-E-EXPERIENCIA.md).

Um repositório de front, três grupos de rota, três regimes de renderização:

| Grupo | Superfície | Render | Motion |
|---|---|---|---|
| `(public)` | portfólio, coleções, serviços | **SSG + ISR**, revalidação por tag no publish | GSAP + Lenis; WebGL opcional por tema |
| `(portal)` | portal do cliente: pedido, briefing, galeria | **SSR** autenticado | Motion para transição; sem WebGL, sem scroll hijack |
| `(studio)` | back-office do fotógrafo | **CSR** puro + TanStack Query | micro-interação apenas |

Back-office que anima é back-office que irrita. A regra está escrita e é para
ser cobrada em revisão.

## 8. Topologia de execução

```
   navegador ──► Vercel (Next 16)  ──► API (Fly.io / ACA)  ──► PostgreSQL 17 (Neon/RDS)
        │                                     │  ▲                    ▲
        │                                     │  │                    │
        └── CDN ──► Cloudflare R2 ◄───────────┘  └── Redis            │
                         ▲                                            │
                         └────────── Worker (Hangfire) ───────────────┘
                                          │
                                          └──► PSP (Asaas) ──► webhook ──► API
```

- Upload de foto vai **do navegador direto para o R2** por URL pré-assinada.
  O arquivo nunca passa pela API. Ver [09](09-GALERIAS-E-ENTREGA.md).
- Entrega de imagem vai **do CDN**, nunca da API.
- Webhook do PSP entra pela API, é verificado, gravado em `payment_event` e
  processado de forma idempotente. Ver [08](08-PAGAMENTOS-SPLIT.md).

## 9. O que está deliberadamente fora

| Fora | Motivo |
|---|---|
| Microsserviços | um dev, meio período |
| CQRS com banco de leitura separado | Dapper na mesma base resolve por anos |
| Event sourcing | auditoria resolve com `AuditLog` + outbox |
| Kubernetes | container gerenciado resolve |
| Kafka / RabbitMQ | Hangfire sobre Postgres + Redis dão conta do volume previsto |
| GraphQL | superfície pequena e conhecida; REST + OpenAPI basta |
| MediatR, AutoMapper | licença comercial (MediatR) e mapeamento mágico que esconde bug (AutoMapper) |
