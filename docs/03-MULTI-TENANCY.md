# 03 · Multi-tenancy

Decisão de fundação. Mudar depois custa uma migração inteira e uma janela de
indisponibilidade. **Vazamento de dado entre tenants é o único risco
classificado como crítico no produto** — foto de casamento aparecendo na
galeria de outro cliente encerra o negócio.

---

## 1. Decisões

| Decisão | Escolha | Nota |
|---|---|---|
| Isolamento de dados | Banco único, schema único, `tenant_id` em toda tabela de negócio | Barato e simples até alguns milhares de tenants |
| Barreira 1 | `HasQueryFilter` global no EF Core | Filtro automático em todo `DbSet` |
| Barreira 2 | **Row Level Security** no PostgreSQL | Protege contra bug de aplicação e acesso direto ao banco. **Não é opcional** |
| Barreira 3 | `TenantId` explícito em todo repositório e query Dapper | Onde o EF não alcança |
| Resolução do tenant | `{slug}.prata.app`; domínio próprio via CNAME fica para a E5 | Ver [ADR-0004](adr/ADR-0004-subdominio-na-v1.md) |
| Storage | Prefixo `tenants/{tenantId}/…` + URL assinada | Bucket **nunca** público |
| Identidade do usuário | Cliente pertence ao tenant, não à plataforma | Chave única é `(tenant_id, email)` |

## 2. Resolução do tenant

Middleware `TenantResolution` roda **antes** da autenticação e injeta
`ITenantContext`. Antes, e não depois: o portfólio público é anônimo e já
precisa saber de que estúdio é a página.

```
  Host: joao-silva.prata.app
        └── slug = "joao-silva" ──► SELECT id, status FROM tenant WHERE slug = @slug
                                    └── ITenantContext { TenantId, Slug, Status, Settings }
```

Ordem no pipeline:

```
1. TenantResolution   ← resolve por Host; 404 se slug não existe
2. ExceptionHandling
3. RateLimit          ← limite por tenant, não global
4. Authentication     ← valida JWT
5. TenantGuard        ← claim tenant_id do token TEM que casar com o Host
6. Authorization
```

O passo 5 é o que impede um token válido do tenant A de operar sobre o tenant
B trocando o subdomínio na URL. Ver
[RN-TEN-002](06-REGRAS-DE-NEGOCIO.md).

### Slugs reservados

O slug ocupa o espaço de subdomínio da plataforma, então não pode colidir com
nomes de infraestrutura. Lista mínima, validada no cadastro
([RN-TEN-003](06-REGRAS-DE-NEGOCIO.md)):

```
www  api  app  admin  auth  login  static  assets  cdn  img  media
mail  smtp  ftp  ns1  ns2  mx  blog  help  suporte  status  docs
s  t  webhook  webhooks  painel  console  plataforma  prata
```

`s` e `t` estão na lista porque são as rotas de galeria compartilhada
(`/s/{token}`) e de portfólio por path (`/t/{slug}`). Ver [10 · API](10-API.md).

Formato: `^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$` — minúsculo, sem underscore,
sem acento, 3 a 40 caracteres. Imutável depois da primeira publicação do
portfólio: mudar slug depois quebra link indexado e link de galeria já
enviado por WhatsApp.

## 3. As três barreiras

### Barreira 1 — Query filter global no EF Core

```csharp
// Infrastructure/Persistence/PrataDbContext.cs
protected override void OnModelCreating(ModelBuilder builder)
{
    foreach (var entityType in builder.Model.GetEntityTypes())
    {
        if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType)) continue;

        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var body = Expression.Equal(
            Expression.Property(parameter, nameof(ITenantOwned.TenantId)),
            Expression.Property(Expression.Constant(_tenantContext), nameof(ITenantContext.TenantId)));

        builder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
    }
}
```

Cobre leitura via EF. **Não** cobre: `IgnoreQueryFilters()`, SQL cru,
Dapper, job de background sem contexto, `ExecuteUpdate`/`ExecuteDelete`.

### Barreira 2 — Row Level Security no PostgreSQL

A rede de segurança. Existe exatamente para o dia em que alguém esquecer a
barreira 1 ou 3.

```sql
-- Padrão aplicado a toda tabela de negócio. DDL completo em 11-MODELO-DE-DADOS.md
ALTER TABLE gallery ENABLE ROW LEVEL SECURITY;
ALTER TABLE gallery FORCE ROW LEVEL SECURITY;   -- vale até para o dono da tabela

CREATE POLICY gallery_tenant_isolation ON gallery
  USING      (tenant_id = current_setting('prata.tenant_id', true)::uuid)
  WITH CHECK (tenant_id = current_setting('prata.tenant_id', true)::uuid);
```

A aplicação seta a variável de sessão no início de cada conexão/transação:

```csharp
await using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT set_config('prata.tenant_id', @tenantId, true)";  // true = local à transação
```

`USING` filtra o que se lê. `WITH CHECK` impede **escrever** linha com
`tenant_id` de outro — sem ele, um `INSERT` malicioso passa.

### Barreira 3 — `TenantId` explícito

Toda assinatura de repositório e toda query Dapper recebe `TenantId`:

```csharp
// Ruim: depende de estado ambiente
Task<IReadOnlyList<OrderListItem>> ListarPedidos(OrderStatus? status);

// Certo: o tenant é parâmetro, não suposição
Task<IReadOnlyList<OrderListItem>> ListarPedidos(TenantId tenantId, OrderStatus? status);
```

## 4. Papéis do banco — a pegadinha nº 1 de RLS

**Superusuário e dono da tabela ignoram RLS por padrão.** Se a API conectar
com o mesmo usuário que roda as migrations, a RLS está ligada e não protege
nada. Já vi produto inteiro achando que tinha RLS.

Duas mitigações, e usamos **as duas**:

| Mitigação | Como |
|---|---|
| Papel de aplicação separado | `prata_app` — `NOBYPASSRLS`, não é dono de nenhuma tabela, só `SELECT/INSERT/UPDATE/DELETE` |
| `FORCE ROW LEVEL SECURITY` | aplica a policy também ao **dono** da tabela, fechando o caso de alguém rodar a API como owner por engano |
| Owner **não** é superusuário em produção | `FORCE` não vale contra superusuário — ver o aviso abaixo |

> **`FORCE` não protege contra superusuário.** Verificado: superusuário ignora
> RLS incondicionalmente, e `FORCE` só alcança o dono da tabela. Portanto
> `prata_owner` **não pode ser superusuário em produção** — em Neon e RDS já
> não é, por padrão. No `docker-compose.yml` de desenvolvimento ele é, porque
> a imagem do Postgres cria o `POSTGRES_USER` como superusuário; é o motivo de
> o teste de isolamento rodar sempre com `prata_app`.

Duas strings de conexão distintas, documentadas em [`.env.example`](../.env.example):

| String | Papel | Usada por |
|---|---|---|
| `ConnectionStrings__Prata` | `prata_app` | API e Worker, em tempo de execução |
| `ConnectionStrings__PrataMigration` | `prata_owner` | `dotnet ef database update`, apenas |

DDL dos dois papéis em
[11 · Modelo de dados](11-MODELO-DE-DADOS.md), seção "Papéis do banco".

## 5. Storage

```
tenants/{tenantId}/galleries/{galleryId}/{photoId}/original.cr3
tenants/{tenantId}/galleries/{galleryId}/{photoId}/web-1600.webp
tenants/{tenantId}/galleries/{galleryId}/{photoId}/thumb-480.webp
tenants/{tenantId}/showcase/{collectionId}/{itemId}/texture-1024.webp
tenants/{tenantId}/exports/{downloadJobId}.zip
```

Regras:

- Bucket **sempre privado**. Nenhum objeto com ACL público
  ([RN-ENT-002](06-REGRAS-DE-NEGOCIO.md)).
- Toda leitura por **URL assinada com TTL curto** (15 min por padrão).
- O `tenantId` no prefixo é conveniência de operação e custo — **não é
  controle de acesso.** Quem autoriza é a API antes de assinar a URL.
- Derivada de portfólio público vai atrás do CDN com URL imutável por hash;
  derivada de galeria privada, nunca.

## 6. Identidade do usuário

O mesmo e-mail pode ser cliente de dois fotógrafos diferentes. A noiva que
contratou o João em 2026 pode contratar a Maria em 2028, com o mesmo Gmail.

| Consequência | Implementação |
|---|---|
| Chave única de usuário | `UNIQUE (tenant_id, email)` — **nunca** `UNIQUE (email)` |
| Login | precisa do tenant no contexto: o subdomínio já dá isso |
| Reset de senha | token escopado ao tenant. Link volta para o subdomínio de origem |
| Um usuário não é global | conta no tenant A e conta no tenant B são registros independentes, com senhas independentes |

`platform.admin` é a exceção: vive na tabela `platform_user`, fora do modelo
de tenant, e **não tem acesso de leitura a resposta de briefing nem a arquivo
de foto** — apenas metadado e agregados. Ver
[RN-TEN-005](06-REGRAS-DE-NEGOCIO.md) e
[12 · Segurança e LGPD](12-SEGURANCA-E-LGPD.md).

## 7. Onde o vazamento acontece de verdade

Armadilhas em ordem de probabilidade. Todas já derrubaram algum SaaS
multi-tenant.

| # | Armadilha | Defesa |
|---|---|---|
| 1 | **Job de background** roda sem `ITenantContext`: o filtro do EF compara com `Guid.Empty` e retorna nada — ou tudo, se o filtro for mal escrito | Job sempre recebe `TenantId` no payload e abre escopo com `ITenantContext` populado. Nunca varre tabela global sem `GROUP BY tenant_id` |
| 2 | **Query Dapper** esquecendo o `WHERE tenant_id` | RLS pega. E revisão de PR olha toda query nova |
| 3 | `IgnoreQueryFilters()` colado de um Stack Overflow | Proibido fora de `Infrastructure/Persistence/Admin/`. Analisador de PR procura a chamada |
| 4 | `ExecuteUpdate` / `ExecuteDelete` — **não aplicam query filter** em todas as versões | Sempre com `.Where(x => x.TenantId == tenantId)` explícito |
| 5 | **Cache com chave sem tenant**: `cache["order-list"]` serve o tenant errado | Prefixo obrigatório: `prata:{tenantId}:order-list` |
| 6 | **URL assinada** gerada antes de checar posse do recurso | Autorização acontece antes de assinar, sempre |
| 7 | API conectando com `prata_owner` | Ver seção 4. Health check da API valida `current_user` no boot e **se recusa a subir** se for o owner |
| 8 | Token do tenant A usado no subdomínio do tenant B | `TenantGuard` compara claim com host resolvido |
| 9 | Migration ou seed criando linha sem `tenant_id` | Coluna `NOT NULL`, sem default |
| 10 | Log ou mensagem de erro devolvendo dado de outro tenant | `ExceptionHandling` nunca devolve detalhe de banco ao cliente |

## 8. Checklist de revisão

Rode isto em todo PR que toca persistência:

- [ ] Toda tabela nova tem `tenant_id uuid NOT NULL` e implementa `ITenantOwned`
- [ ] Toda tabela nova tem `ENABLE` + `FORCE ROW LEVEL SECURITY` e policy na migration
- [ ] Toda query Dapper nova recebe `TenantId` como parâmetro
- [ ] Nenhum `IgnoreQueryFilters()` novo fora de `Persistence/Admin/`
- [ ] Todo `ExecuteUpdate`/`ExecuteDelete` tem `Where` com `TenantId`
- [ ] Toda chave de cache nova tem prefixo de tenant
- [ ] Todo job novo recebe `TenantId` no payload
- [ ] Todo índice novo começa por `tenant_id`
- [ ] Existe teste de integração que tenta ler a linha do tenant vizinho **e falha**

## 9. O teste que não pode desaparecer

`Prata.Api.IntegrationTests/TenantIsolationTests.cs` cria dois tenants com
dados equivalentes e, para cada tabela de negócio, tenta ler e escrever
cruzado. **Todas as tentativas precisam falhar.**

O CI tem um gate específico: se a string `TenantIsolation` desaparecer de
`tests/`, o build quebra — mesmo que todos os outros testes passem. Ver
[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) e
[15 · Estratégia de testes](15-ESTRATEGIA-DE-TESTES.md).

O teste roda com o papel `prata_app` num Postgres real via Testcontainers.
Rodar isso com SQLite in-memory não testa nada: **SQLite não tem RLS.**
