# 11 · Modelo de dados

PostgreSQL 17. Banco único, schema único, `tenant_id` em toda tabela de
negócio. O SQL aqui é **referência** — a migration real é gerada pelo EF Core,
e o que não dá para expressar em `ModelBuilder` (policy de RLS, `REVOKE`,
índice parcial) entra como SQL bruto dentro da migration.

---

## 1. Convenções

| Item | Decisão | Por quê |
|---|---|---|
| Nome de tabela e coluna | `snake_case`, tabela no **singular** | `EFCore.NamingConventions` mapeia automático. Singular porque a linha é uma entidade |
| Chave primária | `uuid`, **UUID v7** gerado na aplicação (`Guid.CreateVersion7()`) | v7 é ordenável por tempo: índice B-tree sem fragmentação, e não expõe contagem como `bigserial` |
| Data e hora | `timestamptz`, **sempre** | `timestamp` sem fuso em produto com evento marcado por horário é bug garantido |
| Dinheiro | `numeric(14,2)` + coluna de moeda `char(3)` | `float`/`double` proibidos em caminho de dinheiro ([RN-FIN-003](06-REGRAS-DE-NEGOCIO.md)) |
| Enum | `text` com `CHECK`, não `enum` nativo do Postgres | adicionar valor a `enum` nativo exige DDL e travava deploy; `text` + `CHECK` é migration trivial |
| Booleano | `boolean NOT NULL DEFAULT false` | nunca nullable — três estados de booleano é bug esperando |
| Texto | `text`, sem `varchar(n)` | no Postgres não há ganho de performance; o limite vai no domínio |
| Documento semiestruturado | `jsonb` | resposta de briefing, payload de webhook, tokens de tema |
| Soft delete | **não usar** por padrão | filtro esquecido em `deleted_at` é vazamento. Exclusão real + `AuditLog`. Exceções documentadas caso a caso |
| Coluna de auditoria | `created_at`, `created_by`, `updated_at`, `updated_by` | em toda tabela de negócio |
| FK | sempre com `ON DELETE RESTRICT` | cascade apagando galeria por acidente é irreversível |

## 2. Papéis do banco

**A pegadinha nº 1 de RLS:** superusuário e **dono da tabela** ignoram Row
Level Security. Se a API conectar com o mesmo papel que roda as migrations, a
RLS está ligada e não protege nada.

```sql
-- ---------------------------------------------------------------------------
-- prata_owner : dono do schema, aplica DDL. USADO SÓ POR MIGRATION.
-- ---------------------------------------------------------------------------
-- NOSUPERUSER é essencial: superusuário ignora RLS mesmo com FORCE.
CREATE ROLE prata_owner LOGIN NOSUPERUSER PASSWORD :'owner_password';
ALTER DATABASE prata OWNER TO prata_owner;

-- ---------------------------------------------------------------------------
-- prata_app : papel de execução da API e do Worker.
--             NOBYPASSRLS e NÃO é dono de nada.
-- ---------------------------------------------------------------------------
CREATE ROLE prata_app LOGIN PASSWORD :'app_password' NOBYPASSRLS;

GRANT CONNECT ON DATABASE prata TO prata_app;
GRANT USAGE  ON SCHEMA public   TO prata_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO prata_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO prata_app;

-- Tabela criada depois já nasce com o grant certo
ALTER DEFAULT PRIVILEGES FOR ROLE prata_owner IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO prata_app;

-- Auditoria é append-only: nem a aplicação pode alterar (RN-AUD-001)
REVOKE UPDATE, DELETE ON audit_log FROM prata_app;

-- prata_app não pode desligar RLS de nada
REVOKE CREATE ON SCHEMA public FROM prata_app;
```

Duas strings de conexão, documentadas em [`.env.example`](../.env.example):
`ConnectionStrings__Prata` (`prata_app`, execução) e
`ConnectionStrings__PrataMigration` (`prata_owner`, só migration).

A API valida no boot que está conectada como `prata_app` e **se recusa a
subir** caso contrário — [RN-TEN-012](06-REGRAS-DE-NEGOCIO.md).

## 3. Row Level Security

Padrão aplicado a **toda** tabela de negócio:

```sql
-- Uma vez por tabela, dentro da migration que a cria
ALTER TABLE gallery ENABLE ROW LEVEL SECURITY;
ALTER TABLE gallery FORCE  ROW LEVEL SECURITY;   -- aplica também ao dono da tabela

CREATE POLICY gallery_tenant_isolation ON gallery
  USING      (tenant_id = current_setting('prata.tenant_id', true)::uuid)
  WITH CHECK (tenant_id = current_setting('prata.tenant_id', true)::uuid);
```

| Cláusula | Protege contra |
|---|---|
| `USING` | **ler** linha de outro tenant |
| `WITH CHECK` | **escrever** linha com `tenant_id` de outro tenant |
| `FORCE` | alguém rodar a aplicação como **dono** da tabela |

> **Verificado empiricamente, e importante:** `FORCE ROW LEVEL SECURITY`
> aplica a policy ao **dono** da tabela, mas **não a superusuário** —
> superusuário ignora RLS incondicionalmente, e nenhuma cláusula muda isso.
>
> Consequência prática: **`prata_owner` não pode ser superusuário em
> produção.** Se for, a barreira 2 vale apenas contra `prata_app`, e uma
> migration ou um script rodado com o owner enxerga todos os tenants.
>
> Em Neon e RDS isso já é o comportamento padrão — não se recebe superusuário
> real. No `docker-compose.yml` de desenvolvimento, porém, a imagem do
> Postgres cria o `POSTGRES_USER` **como superusuário**: em dev, o
> `prata_owner` bypassa a RLS. Por isso o teste de isolamento roda com
> `prata_app`, nunca com o owner ([15](15-ESTRATEGIA-DE-TESTES.md)) — rodá-lo
> com o owner faz o teste passar por engano.

O segundo parâmetro `true` em `current_setting` faz a função devolver `NULL`
em vez de erro quando a variável não foi setada. Com `NULL`, a comparação
resulta em `NULL` e **nenhuma linha passa** — o comportamento seguro:
esquecer de setar o tenant retorna vazio, nunca tudo.

A aplicação seta a variável no escopo da transação:

```sql
SELECT set_config('prata.tenant_id', '018f2c9e-...', true);  -- true = local à transação
```

Verificação obrigatória em teste de integração, com Postgres real:
[RN-TEN-002](06-REGRAS-DE-NEGOCIO.md) e
[15 · Estratégia de testes](15-ESTRATEGIA-DE-TESTES.md). **SQLite não tem
RLS** — testar isso em memória não testa nada.

### Tabelas fora do modelo de tenant

Não recebem RLS de tenant porque não pertencem a nenhum:

`tenant` · `platform_user` · `outbox_message` (tem `tenant_id`, mas é
processada pelo worker em escopo controlado) · `payment_event` (chega do PSP
antes de saber o tenant; recebe `tenant_id` na resolução e a policy é aplicada
depois) · tabelas do Hangfire (schema `hangfire` próprio).

## 4. Tabelas por módulo

Colunas-chave. Toda tabela de negócio tem, implícitos: `id uuid PK`,
`tenant_id uuid NOT NULL`, `created_at`, `created_by`, `updated_at`,
`updated_by`.

### Identidade

| Tabela | Colunas relevantes | Restrições |
|---|---|---|
| `tenant` | `slug`, `name`, `status`, `plan`, `custom_domain`, `suspended_at` | `UNIQUE (slug)`; `slug` casa o regex de [RN-TEN-003](06-REGRAS-DE-NEGOCIO.md) |
| `tenant_settings` | `theme`, `font_pair`, `palette jsonb`, `effects_enabled`, `timezone`, `quote_validity_days`, `gallery_expiration_months`, `deposit_percent`, `balance_due_days` | `1:1` com `tenant` |
| `app_user` | `email`, `password_hash`, `name`, `role`, `is_active`, `last_login_at` | **`UNIQUE (tenant_id, email)`** — [RN-TEN-004](06-REGRAS-DE-NEGOCIO.md) |
| `invite` | `email`, `role`, `token_hash`, `expires_at`, `accepted_at` | `UNIQUE (tenant_id, email)` entre pendentes |
| `platform_user` | `email`, `password_hash`, `name` | fora do modelo de tenant |

`app_user` e não `user`: `user` é palavra reservada no Postgres e obriga a
citar com aspas em toda query Dapper.

### Catálogo

| Tabela | Colunas relevantes |
|---|---|
| `service_type` | `name`, `slug`, `description`, `is_active`, `sort_order` |
| `package` | `service_type_id`, `name`, `price_amount`, `price_currency`, `included_photos`, `hours`, `deliverables jsonb`, `extra_photo_price_amount`, `is_active`, `is_published` |
| `addon` | `name`, `price_amount`, `price_currency`, `is_on_request`, `is_active` |

### Vitrine

| Tabela | Colunas relevantes | Restrições |
|---|---|---|
| `collection` | `slug`, `title`, `description`, `cover_item_id`, `is_published`, `published_at`, `sort_order` | `UNIQUE (tenant_id, slug)` |
| `collection_item` | `collection_id`, `photo_id?`, `storage_key`, `alt_text`, `width`, `height`, `lqip`, `dominant_color`, `sort_order` | `alt_text NOT NULL` — acessibilidade é requisito, não enfeite |
| `page_content` | `page_key`, `content jsonb` | `UNIQUE (tenant_id, page_key)` |
| `seo_meta` | `entity_type`, `entity_id`, `title`, `description`, `og_image_key` | `UNIQUE (tenant_id, entity_type, entity_id)` |

### Comercial

| Tabela | Colunas relevantes | Restrições |
|---|---|---|
| `client` | `name`, `email`, `whatsapp`, `preferred_channel`, `notes` | `UNIQUE (tenant_id, email)` |
| `order` | `client_id`, `service_type_id`, `status`, `desired_date`, `desired_time`, `location`, `subtotal_amount`, `discount_amount`, `total_amount`, `currency`, `confirmed_at`, `event_done_at`, `status_reason` | `status` com `CHECK` na lista de [05](05-MAQUINAS-DE-ESTADO.md) |
| `order_item` | `order_id`, `package_id?`, `addon_id?`, `name_snapshot`, `unit_price_amount`, `quantity`, `total_amount` | snapshot obrigatório — [RN-CAT-004](06-REGRAS-DE-NEGOCIO.md) |
| `quote` | `order_id`, `version`, `valid_until`, `sent_at`, `total_amount`, `notes` | `UNIQUE (order_id, version)` |
| `discount` | `order_id`, `kind`, `value`, `reason` | `CHECK` impede valor > subtotal |

`order` também é palavra reservada — a tabela existe como `"order"` citada, ou
como `customer_order`. **Decisão: `customer_order`**, para nunca precisar de
aspas em Dapper.

### Briefing

| Tabela | Colunas relevantes | Restrições |
|---|---|---|
| `briefing_template` | `service_type_id`, `version`, `is_published` | `UNIQUE (tenant_id, service_type_id, version)` |
| `briefing_question` | `template_id`, `code`, `text`, `help_text`, `question_type`, `is_required`, `is_sensitive`, `sort_order`, `visible_when` | `UNIQUE (template_id, code)` |
| `briefing_option` | `question_id`, `code`, `label`, `sort_order`, `is_suggested_chip` | — |
| `briefing_answer` | `order_id`, `question_id`, `question_text_snapshot`, `question_type_snapshot`, `value jsonb`, `is_sensitive`, `consent_id?`, `answered_at` | `UNIQUE (order_id, question_id)` |
| `briefing_consent` | `order_id`, `scope`, `granted_at`, `revoked_at`, `ip`, `user_agent`, `purpose_text_snapshot` | guarda o texto da finalidade exibido |
| `shoot_sheet` | `order_id`, `storage_key`, `generated_at`, `expires_at` | — |

Índice `GIN` em `briefing_answer.value` só quando aparecer busca por conteúdo
de resposta. Antes disso é índice pago sem uso.

### Agenda

| Tabela | Colunas relevantes | Restrições |
|---|---|---|
| `booking` | `order_id`, `starts_at`, `ends_at`, `travel_buffer_minutes`, `status` | índice de exclusão por sobreposição — ver seção 5 |
| `availability` | `weekday`, `starts_at_time`, `ends_at_time` | — |
| `blackout_date` | `date`, `reason` | `UNIQUE (tenant_id, date)` |

### Financeiro

| Tabela | Colunas relevantes | Restrições |
|---|---|---|
| `payment` | `order_id`, `kind`, `status`, `total_amount`, `currency`, `split_rule_snapshot jsonb`, `platform_fee_amount`, `external_charge_id`, `method` | `kind` ∈ `Deposit`, `Balance`, `Upsell`, `Reactivation` |
| `installment` | `payment_id`, `sequence`, `status`, `amount`, `due_date`, `method`, `external_installment_id`, `confirmed_at`, `settled_at` | `UNIQUE (payment_id, sequence)`; Σ `amount` = `payment.total_amount` — [RN-FIN-004](06-REGRAS-DE-NEGOCIO.md) |
| `payout_account` | `external_recipient_id`, `kyc_status`, `holder_document_masked`, `pix_key_masked`, `bank_masked`, `kyc_updated_at` | **nunca** documento ou conta em claro; só mascarado para exibição |
| `split_rule` | `percent`, `fixed_amount`, `valid_from`, `valid_to`, `created_by` | sem `UPDATE` destrutivo — [RN-FIN-033](06-REGRAS-DE-NEGOCIO.md) |
| `payout` | `payment_id`, `status`, `amount`, `scheduled_at`, `settled_at`, `failure_reason`, `external_payout_id` | — |
| `payment_event` | `external_event_id`, `event_type`, `payload jsonb`, `received_at`, `processed_at`, `process_error`, `discarded_reason` | **`UNIQUE (external_event_id)`** — [RN-FIN-021](06-REGRAS-DE-NEGOCIO.md) |
| `reconciliation_issue` | `payment_id?`, `kind`, `expected jsonb`, `found jsonb`, `opened_at`, `resolved_at`, `resolved_by`, `resolution_note` | baixa só manual — [RN-FIN-051](06-REGRAS-DE-NEGOCIO.md) |

`payout_account` guarda **apenas** o identificador do PSP e dados mascarados
para exibir na tela. O documento e a conta reais ficam no PSP, que faz o KYC.

### Entrega

| Tabela | Colunas relevantes | Restrições |
|---|---|---|
| `gallery` | `order_id`, `status`, `photo_limit`, `selection_deadline`, `expires_at`, `portfolio_consent`, `has_minor`, `archived_at` | `portfolio_consent` ∈ `Sim`, `SomenteSemRosto`, `Nao` |
| `photo` | `gallery_id`, `original_key`, `original_bytes`, `original_hash`, `taken_at`, `width`, `height`, `sort_order`, `is_favorite_by_studio` | `UNIQUE (gallery_id, original_hash)` evita upload duplicado |
| `photo_variant` | `photo_id`, `kind`, `storage_key`, `width`, `height`, `bytes`, `has_watermark`, `generated_at` | `kind` ∈ `Original`, `Thumb`, `Web`, `Texture`, `Lqip`; `UNIQUE (photo_id, kind)` |
| `selection` | `gallery_id`, `photo_id`, `selected_by`, `selected_at` | `UNIQUE (gallery_id, photo_id)` |
| `share_link` | `gallery_id`, `token_hash`, `password_hash`, `expires_at`, `allow_favorites`, `allow_web_download`, `revoked_at`, `last_access_at`, `access_count` | `UNIQUE (token_hash)`; guarda **hash**, nunca o token |
| `download_job` | `gallery_id`, `scope`, `status`, `storage_key`, `bytes`, `expires_at`, `requested_by` | — |

### Transversais

| Tabela | Colunas relevantes | Restrições |
|---|---|---|
| `contract` | `order_id`, `status`, `template_version`, `pdf_key`, `pdf_sha256`, `sent_at`, `viewed_at`, `signed_at`, `expires_at`, `replaces_contract_id` | assinado é imutável — [RN-CTR-011](06-REGRAS-DE-NEGOCIO.md) |
| `signature` | `contract_id`, `signer_name`, `signer_email`, `pdf_sha256`, `ip`, `user_agent`, `signed_at` | os quatro últimos são obrigatórios — [RN-CTR-010](06-REGRAS-DE-NEGOCIO.md) |
| `notification` | `channel`, `recipient`, `kind`, `source_key`, `status`, `attempts`, `last_error`, `sent_at` | **`UNIQUE (tenant_id, recipient, kind, source_key)`** — [RN-NOT-001](06-REGRAS-DE-NEGOCIO.md) |
| `audit_log` | `actor_id`, `actor_role`, `resource_type`, `resource_id`, `action`, `before jsonb`, `after jsonb`, `ip`, `occurred_at` | append-only por `REVOKE` — [RN-AUD-001](06-REGRAS-DE-NEGOCIO.md) |
| `outbox_message` | `type`, `payload jsonb`, `occurred_at`, `processed_at`, `attempts`, `last_error` | índice parcial nos não processados |
| `idempotency_key` | `key`, `endpoint`, `request_hash`, `response jsonb`, `status_code`, `expires_at` | `UNIQUE (tenant_id, key)` |

## 5. Índices

Regra geral: **todo índice de tabela de negócio começa por `tenant_id`**. Sem
isso, o filtro de tenant não usa índice e a query varre a tabela inteira.

```sql
-- Listagem do back-office: o acesso mais frequente do produto
CREATE INDEX ix_order_tenant_status_date
  ON customer_order (tenant_id, status, desired_date DESC);

-- Portal do cliente
CREATE INDEX ix_order_tenant_client
  ON customer_order (tenant_id, client_id, created_at DESC);

-- Portfólio público: só coleção publicada
CREATE INDEX ix_collection_published
  ON collection (tenant_id, sort_order)
  WHERE is_published;

-- Cobrança vencida: alimenta o job de bloqueio de galeria
CREATE INDEX ix_installment_due_open
  ON installment (tenant_id, due_date)
  WHERE status NOT IN ('Confirmado', 'Liquidado', 'Repassado', 'Estornado');

-- Grade da galeria
CREATE INDEX ix_photo_gallery_sort
  ON photo (tenant_id, gallery_id, sort_order);

-- Outbox: só o que falta processar
CREATE INDEX ix_outbox_unprocessed
  ON outbox_message (occurred_at)
  WHERE processed_at IS NULL;

-- Expiração de galeria: alimenta o job de aviso
CREATE INDEX ix_gallery_expiring
  ON gallery (tenant_id, expires_at)
  WHERE status IN ('Disponivel', 'EmSelecao', 'SelecaoFechada', 'Entregue');

-- Idempotência de webhook
CREATE UNIQUE INDEX uq_payment_event_external
  ON payment_event (external_event_id);
```

### Sobreposição de reserva (E5)

O jeito certo de garantir [RN-AGD-001](06-REGRAS-DE-NEGOCIO.md) é deixar o
banco garantir, não a aplicação:

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE booking ADD CONSTRAINT ex_booking_no_overlap
  EXCLUDE USING gist (
    tenant_id WITH =,
    tstzrange(
      starts_at - (travel_buffer_minutes * INTERVAL '1 minute'),
      ends_at   + (travel_buffer_minutes * INTERVAL '1 minute')
    ) WITH &&
  ) WHERE (status = 'Ativo');
```

Checar sobreposição na aplicação perde para condição de corrida: dois pedidos
confirmando na mesma data ao mesmo tempo passam pelos dois `SELECT` e
inserem os dois. `EXCLUDE` não perde.

## 6. `jsonb` no briefing

A alternativa natural é EAV — `answer_value(question_id, field_name,
string_value, int_value, date_value…)` — e é uma armadilha: toda leitura vira
dez `JOIN`, todo tipo novo vira coluna nova, e a validação sai do domínio.

Com `jsonb`:

```json
{ "chips": ["angulo-de-baixo", "bracos"], "text": "prefiro não aparecer de perfil" }
```

| Ponto | Como fica |
|---|---|
| Validação de forma | no domínio, pelo `QuestionTypeSnapshot`. O banco não valida — o agregado valida |
| Consulta por conteúdo | `GIN` em `value`, **só quando** aparecer o caso de uso |
| Migração de formato | nenhuma: o snapshot do tipo diz como ler cada resposta antiga |
| Dado sensível | coluna `is_sensitive` copiada para a resposta, para o filtro de autorização não precisar de `JOIN` |

O `is_sensitive` duplicado é desnormalização deliberada: o filtro de
autorização roda em toda leitura, e depender de `JOIN` com
`briefing_question` para saber se pode mostrar é caro e frágil.

## 7. Migrations

| Regra | Motivo |
|---|---|
| Toda migration roda como `prata_owner` | é quem tem DDL |
| Migration que cria tabela de negócio **precisa** incluir `ENABLE`/`FORCE RLS` + policy no `migrationBuilder.Sql()` | esquecer isso é a falha mais provável do projeto |
| Nenhuma migration destrói coluna com dado em produção sem migration de cópia antes | — |
| Migration é revisada como código | `Migrations/*.cs` está fora do analisador de estilo, não da revisão |
| Seed de template de briefing é migration de dados separada | seed misturado com DDL não dá rollback |

Teste de integração que garante a policy: para cada tabela em
`information_schema.tables` marcada como de negócio, afirmar que existe
policy de RLS. Falha em tabela nova sem policy — ver
[15 · Estratégia de testes](15-ESTRATEGIA-DE-TESTES.md).
