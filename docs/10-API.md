# 10 · Superfície de API

Minimal APIs em ASP.NET Core, um arquivo de endpoints por contexto. OpenAPI
gerado nativamente pelo .NET 10 (`Microsoft.AspNetCore.OpenApi`) e UI via
Scalar — Swashbuckle saiu dos templates.

---

## 1. Convenções

| Item | Decisão |
|---|---|
| Base | `https://api.prata.app` · tenant resolvido pelo header `Host` do front ou pelo path `/t/{slug}` nas rotas públicas |
| Versionamento | prefixo `/v1`. Quebra de contrato só em versão nova; campo novo opcional não quebra |
| Autenticação | `Authorization: Bearer <jwt>` com claims `tenant_id`, `sub`, `role` |
| Formato | JSON, `camelCase`, datas em ISO 8601 com fuso (`2027-05-14T16:00:00-03:00`) |
| Dinheiro | objeto `{ "amount": 8000.00, "currency": "BRL" }`. **Nunca** número solto nem centavos implícitos |
| Erro | RFC 9457 `application/problem+json` |
| Idempotência | header `Idempotency-Key` obrigatório em `POST` que cria dinheiro ou recurso externo |
| Paginação | cursor: `?cursor=<opaco>&limit=50`. Resposta traz `nextCursor`. Sem `offset` — lista de pedidos cresce e `OFFSET 10000` é varredura |
| Ordenação | `?sort=-createdAt` (`-` = descendente), campos permitidos por endpoint |
| Rate limit | por tenant e por papel. `429` com `Retry-After` |
| Correlação | `X-Correlation-Id` aceito e ecoado; gerado se ausente |

### Formato de erro

```json
{
  "type": "https://prata.app/errors/pedido/transicao-invalida",
  "title": "Transição de estado inválida",
  "status": 409,
  "detail": "Pedido em 'Enviado' não pode ir para 'Confirmado'.",
  "instance": "/v1/orders/018f.../confirm",
  "code": "PEDIDO_TRANSICAO_INVALIDA",
  "correlationId": "01J8X...",
  "errors": {
    "status": ["origem: Enviado", "destino: Confirmado"]
  }
}
```

- `code` é estável e é o que o front usa para traduzir mensagem. `title` e
  `detail` são para humano e podem mudar.
- `errors` só aparece em erro de validação ou com contexto estruturado.
- **`detail` nunca contém dado de outro tenant, mensagem do banco ou stack.**
  Ver [RN-TEN-002](06-REGRAS-DE-NEGOCIO.md).

## 2. Superfície

Coluna **Etapa** indica quando o endpoint existe. `tenant.*` = owner e staff.

### Público — sem autenticação

| Método | Rota | O quê | Acesso | Etapa |
|---|---|---|---|---|
| `GET` | `/v1/t/{slug}/portfolio` | coleções públicas (cacheável, alimenta o ISR) | `anon` | E1 |
| `GET` | `/v1/t/{slug}/portfolio/{collectionSlug}` | itens de uma coleção | `anon` | E1 |
| `GET` | `/v1/t/{slug}/services` | tipos de serviço e pacotes ativos | `anon` | E1 |
| `GET` | `/v1/t/{slug}/theme` | tokens de tema do tenant (cor, fonte, `effectsEnabled`) | `anon` | E1 |
| `GET` | `/v1/t/{slug}/sitemap.xml` | sitemap por tenant | `anon` | E1 |
| `POST` | `/v1/auth/register` | cadastro simples do cliente (nome, e-mail, WhatsApp) | `anon` | E2 |
| `POST` | `/v1/auth/login` | login no tenant do host | `anon` | E1 |
| `POST` | `/v1/auth/refresh` | renova access token | `anon` | E1 |
| `POST` | `/v1/auth/password-reset` | inicia recuperação, escopada ao tenant | `anon` | E1 |

### Portal do cliente — `client`

| Método | Rota | O quê | Etapa |
|---|---|---|---|
| `POST` | `/v1/orders` | abre pedido: serviço + data pretendida | E2 |
| `GET` | `/v1/orders` | pedidos do próprio cliente | E2 |
| `GET` | `/v1/orders/{id}` | detalhe do pedido | E2 |
| `GET` | `/v1/orders/{id}/briefing` | template de perguntas do serviço, já com respostas salvas | E2 |
| `PUT` | `/v1/orders/{id}/briefing` | salva respostas (parcial, autosave) | E2 |
| `POST` | `/v1/orders/{id}/briefing/consent` | consentimento específico do bloco sensível | E2 |
| `POST` | `/v1/orders/{id}/briefing/upload-url` | URL assinada para upload de referência (B8, D2) | E2 |
| `POST` | `/v1/orders/{id}/submit` | `Rascunho` → `Enviado` | E2 |
| `POST` | `/v1/orders/{id}/approve` | cliente aprova o orçamento | E2 |
| `POST` | `/v1/orders/{id}/cancel` | cancelamento pelo cliente, com política de reembolso | E3 |
| `POST` | `/v1/orders/{id}/payments` | gera cobrança do sinal com split | E3 |
| `GET` | `/v1/orders/{id}/payments` | parcelas e situação de cada uma | E3 |
| `GET` | `/v1/orders/{id}/contract` | contrato para leitura | E5 |
| `POST` | `/v1/orders/{id}/contract/sign` | aceite: registra hash, IP, user-agent, timestamp | E5 |
| `GET` | `/v1/galleries/{id}` | galeria do cliente | E4 |
| `POST` | `/v1/galleries/{id}/favorites` | marca/desmarca favorita | E4 |
| `POST` | `/v1/galleries/{id}/selection` | fecha seleção de favoritas | E4 |
| `POST` | `/v1/galleries/{id}/download` | pede ZIP → `202` com `downloadJobId` | E4 |
| `GET` | `/v1/download-jobs/{id}` | situação do ZIP | E4 |
| `POST` | `/v1/galleries/{id}/share-links` | cria link para convidado | E4 |

### Back-office do estúdio

| Método | Rota | O quê | Acesso | Etapa |
|---|---|---|---|---|
| `GET` | `/v1/studio/orders` | lista de pedidos com filtro por status | `tenant.*` | E2 |
| `GET` | `/v1/studio/orders/{id}` | detalhe, incluindo briefing | `tenant.*` | E2 |
| `POST` | `/v1/studio/orders/{id}/analyze` | `Enviado` → `EmAnalise` | `tenant.*` | E2 |
| `POST` | `/v1/studio/orders/{id}/quote` | envia orçamento | `tenant.owner` | E2 |
| `POST` | `/v1/studio/orders/{id}/decline` | recusa, com motivo | `tenant.*` | E2 |
| `POST` | `/v1/studio/orders/{id}/hold` | coloca em espera, com motivo | `tenant.*` | E2 |
| `GET` | `/v1/studio/orders/{id}/shoot-sheet` | ficha de direção em PDF (URL assinada) | `tenant.*` | E2 |
| `POST` | `/v1/studio/orders/{id}/reschedule` | reagenda | `tenant.owner` | E5 |
| `GET` | `/v1/studio/clients` | carteira de clientes | `tenant.*` | E2 |
| `GET`/`POST`/`PUT` | `/v1/studio/service-types` | tipos de serviço | `tenant.owner` | E1 |
| `GET`/`POST`/`PUT` | `/v1/studio/packages` | pacotes e preços | `tenant.owner` | E1 |
| `GET`/`POST`/`PUT` | `/v1/studio/collections` | coleções do portfólio | `tenant.*` | E1 |
| `POST` | `/v1/studio/collections/{id}/publish` | publica e revalida o ISR | `tenant.*` | E1 |
| `GET`/`PUT` | `/v1/studio/briefing-templates` | edita o banco de perguntas | `tenant.owner` | E2 |
| `GET`/`PUT` | `/v1/studio/settings` | tema, fontes, prazos, `effectsEnabled` | `tenant.owner` | E1 |
| `GET`/`POST` | `/v1/studio/team` | equipe e convites | `tenant.owner` | E1 |
| `GET` | `/v1/studio/finance` | a receber, liquidado, repassado, comissão | **`tenant.owner`** | E3 |
| `GET` | `/v1/studio/finance/reconciliation` | divergências abertas | **`tenant.owner`** | E3 |
| `GET`/`POST` | `/v1/studio/payout-account` | cadastro/atualização da conta de repasse | **`tenant.owner`** | E3 |
| `GET` | `/v1/studio/payout-account/kyc` | situação do KYC no PSP | **`tenant.owner`** | E3 |
| `POST` | `/v1/studio/galleries` | cria galeria do pedido | `tenant.*` | E4 |
| `POST` | `/v1/studio/galleries/{id}/upload-url` | URL pré-assinada para upload direto | `tenant.*` | E4 |
| `POST` | `/v1/studio/galleries/{id}/photos/{photoId}/complete` | finaliza multipart e enfileira derivadas | `tenant.*` | E4 |
| `POST` | `/v1/studio/galleries/{id}/publish` | `EmPreparo` → `Disponivel` | `tenant.*` | E4 |
| `GET`/`POST` | `/v1/studio/availability` | disponibilidade e bloqueios | `tenant.*` | E5 |

Endpoint marcado com `tenant.owner` em negrito retorna `403` para
`tenant.staff` — ver [RN-TEN-006](06-REGRAS-DE-NEGOCIO.md).

### Galeria compartilhada — convidado, sem conta

| Método | Rota | O quê | Etapa |
|---|---|---|---|
| `POST` | `/v1/s/{token}/unlock` | valida senha, cria sessão de convidado no Redis | E4 |
| `GET` | `/v1/s/{token}` | galeria compartilhada (só derivadas com marca d'água) | E4 |
| `POST` | `/v1/s/{token}/favorites` | favoritar, se o estúdio permitir | E4 |

### Webhooks e console da plataforma

| Método | Rota | O quê | Acesso | Etapa |
|---|---|---|---|---|
| `POST` | `/v1/webhooks/psp` | eventos do PSP — **assinatura verificada e idempotente** | assinado | E3 |
| `POST` | `/v1/internal/revalidate` | revalidação de ISR do front | segredo compartilhado | E1 |
| `GET` | `/v1/platform/tenants` | tenants, plano e situação | `platform.admin` | E1 |
| `POST` | `/v1/platform/tenants/{id}/suspend` | suspende tenant | `platform.admin` | E1 |
| `GET`/`PUT` | `/v1/platform/tenants/{id}/split-rule` | comissão por tenant, versionada | `platform.admin` | E3 |
| `GET` | `/v1/platform/reconciliation` | divergências de todos os tenants | `platform.admin` | E3 |
| `GET` | `/v1/platform/audit` | consulta da trilha de auditoria | `platform.admin` | E1 |
| `GET` | `/health` · `/health/ready` | liveness e readiness | `anon` | E1 |

`platform.admin` **não** tem endpoint que devolva resposta de briefing ou URL
de foto — ver [RN-TEN-005](06-REGRAS-DE-NEGOCIO.md).

## 3. Idempotência

Obrigatória em `POST` que cria dinheiro ou recurso externo:

```
POST /v1/orders/{id}/payments
Idempotency-Key: 018f2c9e-...   (UUID gerado pelo cliente)
```

| Situação | Resposta |
|---|---|
| Chave nova | processa e guarda `(tenant, chave) → resposta` por 24 h |
| Chave repetida, mesmo corpo | devolve a resposta original, com `Idempotency-Replayed: true` |
| Chave repetida, corpo diferente | `422` com `code: IDEMPOTENCY_KEY_REUSADA` |
| Chave ausente onde é exigida | `400` com `code: IDEMPOTENCY_KEY_OBRIGATORIA` |

Motivo concreto: o cliente aperta "pagar" duas vezes num 4G ruim. Sem
idempotência, isso são duas cobranças de sinal.

## 4. Catálogo de códigos de erro

O `code` é estável e traduzido pelo front. Corresponde ao `Error.Code` do
`Result` no domínio — ver [02 · Arquitetura](02-ARQUITETURA.md).

| `code` | HTTP | Quando |
|---|---|---|
| `VALIDACAO_FALHOU` | 400 | FluentValidation reprovou; `errors` traz os campos |
| `IDEMPOTENCY_KEY_OBRIGATORIA` | 400 | falta o header onde é exigido |
| `NAO_AUTENTICADO` | 401 | token ausente, inválido ou expirado |
| `WEBHOOK_ASSINATURA_INVALIDA` | 401 | assinatura do PSP não confere ([RN-FIN-020](06-REGRAS-DE-NEGOCIO.md)) |
| `SEM_PERMISSAO` | 403 | papel não autoriza a operação |
| `TENANT_DIVERGENTE` | 403 | `tenant_id` do token ≠ tenant do host ([RN-TEN-011](06-REGRAS-DE-NEGOCIO.md)) |
| `RECURSO_NAO_ENCONTRADO` | 404 | id inexistente **ou de outro tenant** — nunca se distingue os dois casos |
| `TENANT_NAO_ENCONTRADO` | 404 | slug não existe |
| `PEDIDO_TRANSICAO_INVALIDA` | 409 | transição fora da tabela de [05](05-MAQUINAS-DE-ESTADO.md) |
| `SINAL_NAO_CONFIRMADO` | 409 | tentativa de confirmar sem sinal ([RN-COM-020](06-REGRAS-DE-NEGOCIO.md)) |
| `CONTRATO_NAO_ASSINADO` | 409 | idem, sem contrato |
| `ORCAMENTO_EXPIRADO` | 409 | aprovação fora da validade |
| `DATA_INDISPONIVEL` | 409 | conflito de agenda ([RN-AGD-001](06-REGRAS-DE-NEGOCIO.md)) |
| `SELECAO_ACIMA_DO_LIMITE` | 409 | fechar seleção com excedente não pago ([RN-ENT-020](06-REGRAS-DE-NEGOCIO.md)) |
| `GALERIA_BLOQUEADA_POR_PENDENCIA` | 409 | download com saldo em aberto ([RN-ENT-032](06-REGRAS-DE-NEGOCIO.md)) |
| `ORIGINAL_NAO_LIBERADO` | 409 | original antes de `SelecaoFechada` + saldo ([RN-ENT-021](06-REGRAS-DE-NEGOCIO.md)) |
| `REPASSE_BLOQUEADO_KYC` | 409 | repasse com KYC pendente ([RN-FIN-030](06-REGRAS-DE-NEGOCIO.md)) |
| `CONSENTIMENTO_AUSENTE` | 409 | bloco sensível sem consentimento ([RN-BRF-020](06-REGRAS-DE-NEGOCIO.md)) ou item de portfólio sem autorização ([RN-VIT-005](06-REGRAS-DE-NEGOCIO.md)) |
| `ULTIMO_OWNER` | 409 | remover o último `tenant.owner` ([RN-TEN-007](06-REGRAS-DE-NEGOCIO.md)) |
| `SLUG_INDISPONIVEL` | 409 | slug em uso ou reservado ([RN-TEN-003](06-REGRAS-DE-NEGOCIO.md)) |
| `IDEMPOTENCY_KEY_REUSADA` | 422 | mesma chave, corpo diferente |
| `ARQUIVO_MUITO_GRANDE` | 413 | acima de `Storage__MaxUploadSizeMb` |
| `LIMITE_DE_REQUISICOES` | 429 | rate limit; traz `Retry-After` |
| `ERRO_INTERNO` | 500 | falha não prevista. `detail` genérico, `correlationId` presente |
| `PSP_INDISPONIVEL` | 502 | PSP fora; a operação pode ser retentada |

**Nota sobre `RECURSO_NAO_ENCONTRADO`:** id de outro tenant retorna `404`, não
`403`. Devolver `403` confirmaria a existência do recurso e transformaria a
API num oráculo de enumeração.

## 5. Contrato do webhook do PSP

```
POST /v1/webhooks/psp
asaas-access-token: <segredo combinado>
Content-Type: application/json
```

Ordem de processamento, sem exceção:

```
1. Verificar assinatura            → inválida: 401, NADA é gravado
2. Extrair external_event_id       → ausente: 400
3. INSERT em payment_event         → conflito no UNIQUE: 200, ignora (reenvio)
4. Responder 200 imediatamente
5. Processar em background         → traduzir para evento de domínio
6. Evento antigo demais            → grava, marca como descartado, não regride estado
```

Regras: [RN-FIN-020](06-REGRAS-DE-NEGOCIO.md),
[RN-FIN-021](06-REGRAS-DE-NEGOCIO.md),
[RN-FIN-022](06-REGRAS-DE-NEGOCIO.md),
[RN-FIN-023](06-REGRAS-DE-NEGOCIO.md).

O passo 4 vem antes do 5 de propósito: PSP que recebe timeout reenfileira o
evento e multiplica o problema exatamente no momento de pico.

## 6. Rate limit

| Escopo | Limite padrão |
|---|---|
| `anon` por IP | 60 req/min |
| autenticado por usuário | 300 req/min |
| `ShareLink` por token | 20 req/min |
| webhook do PSP | 600 req/min |
| upload-url por tenant | 120 req/min |

O limite do `ShareLink` é o mais importante: link vazado num grupo de
WhatsApp não pode virar varredura da galeria inteira.
