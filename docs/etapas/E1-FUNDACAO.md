# E1 · Fundação

**Marco:** um fotógrafo real usa o Prata como site oficial dele.
**Ordem de grandeza:** 4 a 6 semanas, meio período.

A etapa termina quando o fotógrafo piloto **troca o link da bio do Instagram**
pelo novo site. Todo o resto é pré-requisito disso.

---

## 1. Escopo

| Entra | Não entra |
|---|---|
| Tenant, `TenantSettings`, tema | pedido, briefing (E2) |
| Auth: registro, login, refresh, reset | pagamento (E3) |
| RLS + papéis de banco + teste de isolamento | galeria e upload (E4) |
| Resolução de tenant por subdomínio | agenda e contrato (E5) |
| Equipe e convites | domínio próprio (E5) |
| Catálogo: `ServiceType`, `Package`, `Addon` | tema `cinema` e `imersivo` (E4) |
| Vitrine: `Collection`, `CollectionItem`, SEO | WhatsApp Cloud API (E5) |
| Portfólio público com ISR, tema `editorial` | |
| Console da plataforma: listar e suspender tenant | |
| Observabilidade: log, trace, health check | |

## 2. Antes de escrever código

Três itens que não dependem de programação e travam etapas futuras se ficarem
para depois.

| # | Tarefa | Por quê agora |
|---|---|---|
| 1 | **Abrir conta no Asaas e solicitar habilitação de split/marketplace** | É análise comercial e de compliance do PSP, leva **semanas**, e não depende de código. Descobrir isso quando a E3 começar custa um mês de espera com o produto pronto. Ver [ADR-0005](../adr/ADR-0005-psp-asaas-com-port.md) |
| 2 | Registrar o domínio da plataforma e configurar DNS wildcard `*.prata.app` | O portfólio público da E1 não existe sem isso |
| 3 | Termo de uso e política de privacidade, com o enquadramento controlador/operador | Precisa de advogado. O portfólio vai ao ar com dado de cliente. Ver [12](../12-SEGURANCA-E-LGPD.md), seção 7 |

Enquanto o item 1 tramita, a E1 e a E2 rodam normalmente.

## 3. Tarefas

### 3.1 Esqueleto da solução

```bash
dotnet new sln -n Prata
dotnet new classlib -o src/Prata.Domain
dotnet new classlib -o src/Prata.Application
dotnet new classlib -o src/Prata.Infrastructure
dotnet new web      -o src/Prata.Api
dotnet new worker   -o src/Prata.Worker
dotnet new xunit3   -o tests/Prata.Domain.Tests
dotnet new xunit3   -o tests/Prata.Application.Tests
dotnet new xunit3   -o tests/Prata.Api.IntegrationTests
# referências seguindo a regra de dependência de docs/02-ARQUITETURA.md
```

- [ ] Solution e projetos criados, com a regra de dependência respeitada
- [ ] `Directory.Build.props` e `Directory.Packages.props` já no lugar (prontos)
- [ ] **Teste de arquitetura** falhando o build se `Domain` referenciar algo
- [ ] `Domain/Common`: `Entity`, `AggregateRoot`, `ValueObject`, `DomainEvent`,
      `Result`, `Error`, `Money`, `ITenantOwned`
- [ ] `Application/Abstractions`: os ports, ainda sem implementação
- [ ] Dispatcher próprio + pipeline de behaviors ([02](../02-ARQUITETURA.md), 5.1)

### 3.2 Front-end: esqueleto

```bash
npx create-next-app@latest web --typescript --tailwind --app --no-src-dir
```

- [ ] Next 16 + React 19 + TypeScript 7 + Tailwind v4
- [ ] Três grupos de rota: `(public)`, `(portal)`, `(studio)`
- [ ] `size-limit` configurado no `package.json` com os limites de
      [RN-FRT-001](../06-REGRAS-DE-NEGOCIO.md)
- [ ] shadcn/ui inicializado
- [ ] Fontes variáveis self-hosted, os quatro pares de
      [16](../16-FRONTEND-E-EXPERIENCIA.md), seção 6
- [ ] Sentry com Web Vitals reportando `tenant_slug`

### 3.3 Multi-tenancy — o núcleo da etapa

- [ ] `Tenant`, `TenantSettings` com invariantes de
      [04](../04-MODULOS-E-AGREGADOS.md)
- [ ] Validação de slug: regex, lista de reservados, imutabilidade após
      publicar ([RN-TEN-003](../06-REGRAS-DE-NEGOCIO.md))
- [ ] `ITenantContext` + middleware `TenantResolution` **antes** da autenticação
- [ ] `HasQueryFilter` global automático para `ITenantOwned`
- [ ] Migration com `ENABLE`/`FORCE ROW LEVEL SECURITY` + policy em **toda**
      tabela de negócio
- [ ] `scripts/db-roles.sql` aplicado: `prata_owner` e `prata_app`
- [ ] Duas strings de conexão, separadas
- [ ] **A API se recusa a subir** conectada como owner
      ([RN-TEN-012](../06-REGRAS-DE-NEGOCIO.md))
- [ ] `TenantGuard` comparando claim `tenant_id` com o host
      ([RN-TEN-011](../06-REGRAS-DE-NEGOCIO.md))
- [ ] **`TenantIsolationTests`** completo, contra Postgres real
      ([15](../15-ESTRATEGIA-DE-TESTES.md), seção 5)
- [ ] Teste que varre `information_schema` e falha em tabela sem policy

### 3.4 Identidade e acesso

- [ ] ASP.NET Core Identity com `UNIQUE (tenant_id, email)`
      ([RN-TEN-004](../06-REGRAS-DE-NEGOCIO.md))
- [ ] JWT com `tenant_id` e `role`; refresh rotativo
- [ ] Lockout: 5 falhas / 15 min ([RN-TEN-010](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Reset de senha escopado ao tenant, sem enumeração de e-mail
- [ ] `Invite` com expiração de 7 dias
      ([RN-TEN-008](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Bloqueio de remoção do último owner
      ([RN-TEN-007](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Autorização por papel nos endpoints, com testes das três linhas críticas
      de [12](../12-SEGURANCA-E-LGPD.md), seção 1

### 3.5 Catálogo

- [ ] `ServiceType`, `Package`, `Addon` com as invariantes de
      [04](../04-MODULOS-E-AGREGADOS.md)
- [ ] Seed dos dez tipos de serviço na criação do tenant
- [ ] Publicação de pacote exigindo campos completos
      ([RN-CAT-003](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Desativar pacote não afeta pedido existente
      ([RN-CAT-005](../06-REGRAS-DE-NEGOCIO.md)) — testado desde já, mesmo
      sem pedido na E1
- [ ] CRUD no back-office

### 3.6 Vitrine e portfólio público

- [ ] `Collection`, `CollectionItem`, `PageContent`, `SeoMeta`
- [ ] `alt_text NOT NULL` no banco
- [ ] Publicação exigindo ≥ 1 item e capa
      ([RN-VIT-001](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Revalidação de ISR por tag no publish
      ([RN-VIT-004](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Upload de imagem de portfólio por URL assinada, com derivadas
      (`thumb`, `web` **sem** marca d'água, `texture`, `lqip`) —
      antecipa parte do worker da E4
- [ ] `sitemap.xml` e `robots.txt` por tenant
      ([RN-VIT-007](../06-REGRAS-DE-NEGOCIO.md))
- [ ] JSON-LD: `LocalBusiness` + `ImageObject`
- [ ] OG image com `@vercel/og`
- [ ] **Tema `editorial`**: GSAP reveal + parallax leve, Lenis com as guardas
      de [RN-FRT-005](../06-REGRAS-DE-NEGOCIO.md). **Sem WebGL nesta etapa**
- [ ] Tokens de tenant por CSS custom properties, servidos por
      `GET /v1/t/{slug}/theme`

### 3.7 Operação

- [ ] Serilog → Seq com `correlation_id`, `tenant_id`, `user_id`
- [ ] Filtro de segredo no log ([12](../12-SEGURANCA-E-LGPD.md), seção 3)
- [ ] OpenTelemetry: ASP.NET Core, HTTP, Npgsql
- [ ] `/health` e `/health/ready`
- [ ] Rate limit por tenant e por papel
- [ ] `ExceptionHandling` devolvendo ProblemDetails, **sem** detalhe de banco
- [ ] Outbox + worker processando
- [ ] CI verde: docs, backend, front, Lighthouse
- [ ] Deploy: front na Vercel com wildcard, API em contêiner, banco gerenciado

### 3.8 Console da plataforma

- [ ] `platform_user` fora do modelo de tenant
- [ ] Listar tenants, suspender e reativar
      ([RN-TEN-009](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Consulta de `AuditLog`
- [ ] **Teste de que `platform.admin` não lê briefing nem foto**
      ([RN-TEN-005](../06-REGRAS-DE-NEGOCIO.md)) — mesmo sem briefing na E1,
      o endpoint já nasce negado

## 4. Critério de aceite

> Código na branch `feat/e5-agenda`. Itens de piloto/produção abertos.

- [ ] Um tenant real publicado em `{slug}.prata.app` com o portfólio dele
- [x] `TenantIsolationTests` passando contra Postgres real *(Testcontainers)*
- [x] A API se recusa a subir se conectar como dono das tabelas
- [ ] Lighthouse CI verde: LCP ≤ 2,0 s · CLS ≤ 0,05 · SEO 100 · a11y ≥ 95
- [ ] `size-limit` verde: rota pública ≤ 120 kB gzip
- [ ] Conta no PSP aberta e **habilitação de split solicitada**
- [ ] Termo de uso e política de privacidade publicados
- [ ] **O fotógrafo piloto trocou o link da bio do Instagram**

## 5. Riscos da etapa

| Risco | Mitigação |
|---|---|
| RLS "ligada" mas inoperante por a API rodar como owner | recusa de boot + `TenantIsolationTests` com o papel `prata_app` |
| Migration nova sem policy de RLS | teste que varre `information_schema` |
| Habilitação de split demorar mais que o previsto | solicitada na E1; a E2 não depende dela |
| Tema `editorial` estourar o orçamento de performance | Lighthouse CI e size-limit desde o primeiro commit de `web/`, não no fim |
| Fotógrafo piloto desistir | conseguir **dois** pilotos, não um |
