# Configurar Asaas no Prata (sandbox primeiro)

Checklist operacional focado em **sandbox**. Decisão de produto: [ADR-0005](../adr/ADR-0005-psp-asaas-com-port.md). Modelo de split: [08](../08-PAGAMENTOS-SPLIT.md).

**Prioridade deste guia:** conta e webhook no [sandbox.asaas.com](https://sandbox.asaas.com). Produção só no final, como nota.

O Prata sobe `AsaasPaymentGateway` só se `Payments:Provider=Asaas` **e** `ApiKey` + `WebhookSecret` forem valores reais (não `CHANGE_ME`). Caso contrário usa `FakePaymentGateway` — ver `AddPrataPaymentGateway` / `ShouldUseAsaas`.

---

## Nomes sugeridos (sandbox)

Rótulos no painel — **não** são secrets. Gere ApiKey/token no Asaas; não invente valores aqui.

| O quê | Nome sugerido |
|---|---|
| Conta Asaas da plataforma | `prata-sandbox` |
| Label da API Key | `prata-api-sandbox` |
| Nome do webhook | `webhook-prata-sandbox` |
| Lembrete do token | `asaas-access-token → Payments__Asaas__WebhookSecret` |
| Wallet da plataforma | wallet da própria conta `prata-sandbox` → `Payments__Asaas__WalletIdPlatform` |

Não há staging Asaas separado: preview/PR também usa esta conta sandbox ([14](../14-AMBIENTES-E-OPERACAO.md)).

---

## 1. Conta sandbox

| | |
|---|---|
| Painel | [sandbox.asaas.com](https://sandbox.asaas.com) |
| API (BaseUrl do Prata) | `https://api-sandbox.asaas.com/v3` |

1. Crie a conta **da plataforma** (CNPJ/CPF da empresa Prata), não a do fotógrafo. Nome sugerido: `prata-sandbox`.
2. Use só chaves e wallet desta conta com `BaseUrl` sandbox.
3. Produção é outra conta, outra ApiKey, outro webhook — não misture.

---

## 2. ApiKey e token do webhook

No painel **sandbox**:

1. **Integrações → API Key** → gerar (label: `prata-api-sandbox`). Copie uma vez; não commitar.
2. **Integrações → Webhooks** → defina um **token de acesso** (segredo longo, aleatório).
   - O Asaas envia esse valor no header `asaas-access-token`.
   - No Prata: `Payments__Asaas__WebhookSecret` (RN-FIN-020).

Nunca use a ApiKey como token do webhook.

---

## 3. Marketplace / split

Split com subcontas exige habilitação **marketplace** na conta da plataforma. Análise comercial do PSP — leva dias/semanas ([E1](../etapas/E1-FUNDACAO.md) item 1).

1. No painel sandbox (ou suporte Asaas), solicite **marketplace / split de pagamento**.
2. Enquanto **pendente**: código e testes com `FakePaymentGateway` ou sandbox sem split real; não marque aceite de produção ([E3](../etapas/E3-DINHEIRO.md), [13](../13-ROADMAP-E-RISCOS.md)).
3. Quando **aprovado**, confirme que dá para criar subcontas (`walletId`) e cobranças com `split`.

---

## 4. Wallet da plataforma (`WalletIdPlatform`)

1. Na conta `prata-sandbox`, obtenha o **walletId** da própria conta.
2. Coloque em `Payments__Asaas__WalletIdPlatform`.
3. É a wallet da **comissão** da plataforma — não a do fotógrafo.
4. Na cobrança, o adapter envia `split` com `walletId` do recebedor (fotógrafo); o restante fica na conta que emitiu a cobrança.

Fotógrafo: onboarding via `CriarRecebedor` → `walletId` em `PayoutAccount` (não configure wallet de tenant no `.env`).

---

## 5. Variáveis de ambiente (sandbox)

Placeholders só com `CHANGE_ME` — nunca secrets reais no git.

```bash
Payments__Provider=Asaas
Payments__Asaas__BaseUrl=https://api-sandbox.asaas.com/v3
Payments__Asaas__ApiKey=CHANGE_ME
Payments__Asaas__WebhookSecret=CHANGE_ME
Payments__Asaas__WalletIdPlatform=CHANGE_ME
```

| Variável | Obrigatória para Asaas real? | Notas |
|---|---|---|
| `Payments__Provider` | sim (`Asaas`) | `Fake` força fake |
| `Payments__Asaas__BaseUrl` | recomendada | default sandbox se vazia |
| `Payments__Asaas__ApiKey` | sim | header `access_token` nas calls à API Asaas |
| `Payments__Asaas__WebhookSecret` | sim | deve bater com `asaas-access-token` |
| `Payments__Asaas__WalletIdPlatform` | sim em operação | wallet da comissão |

Referência: [`.env.example`](../../.env.example) · [deploy-e1.md](../deploy-e1.md).

---

## 6. Webhook — URL de sincronização (sandbox)

Dois hosts diferentes — não confunda:

| | URL | Uso |
|---|---|---|
| **API Asaas (saída)** | `https://api-sandbox.asaas.com/v3` | `Payments__Asaas__BaseUrl` — Prata → Asaas |
| **Webhook (entrada)** | URL **pública do Prata** + path abaixo | painel sandbox → Prata |

Endpoint do Prata: **`POST /v1/webhooks/asaas`** (anônimo; auth = header `asaas-access-token`).

No painel sandbox o campo costuma ser **URL** / **URL de notificação** / **URL de sincronização**. Cadastre a URL **do Prata** (HTTPS, path completo, sem query string):

| Onde a API Prata roda | URL a cadastrar no Asaas sandbox |
|---|---|
| Local (túnel) | `https://<seu-tunel>/v1/webhooks/asaas` |
| Preview / PR | `https://<host-efemero-da-api>/v1/webhooks/asaas` |

Exemplo local:

```bash
# ver docs/14-AMBIENTES-E-OPERACAO.md
cloudflared tunnel --url http://localhost:5080
# se o túnel for https://abc.trycloudflare.com → cadastre:
# https://abc.trycloudflare.com/v1/webhooks/asaas
```

No painel **sandbox.asaas.com**:

1. Nome do webhook: `webhook-prata-sandbox`.
2. **URL** = uma das linhas da tabela (não use `api-sandbox.asaas.com` aqui).
3. Token = mesmo valor de `Payments__Asaas__WebhookSecret`.
4. Header: `asaas-access-token: <WebhookSecret>`.
5. **Eventos a marcar** (`WebhookPaymentProcessor`):
   - `PAYMENT_RECEIVED`, `PAYMENT_CONFIRMED` → confirma parcela
   - `PAYMENT_SETTLED` (se existir no painel) → liquida parcela
   - Demais → gravados e descartados (`tipo_nao_tratado`)

Token errado → **401** `WEBHOOK_ASSINATURA_INVALIDA`.

---

## 7. Validar (sandbox)

### A. Só Fake (dev sem Asaas)

- `ApiKey`/`WebhookSecret` = `CHANGE_ME` **ou** `Provider=Fake`.
- Fake espera `asaas-access-token: sandbox-ok` e body no formato interno (não JSON Asaas).

### B. Adapter Asaas sandbox

1. Preencha ApiKey + WebhookSecret reais do **sandbox**.
2. Reinicie API; chamadas HTTP devem ir para `api-sandbox.asaas.com`.
3. Smoke: cobrança Pix → pague no sandbox → webhook `200` + `payment_event`; reenvio → idempotente (RN-FIN-021).
4. Token errado → **401**.
5. **Não** bata API real do PSP no CI ([08 §9](../08-PAGAMENTOS-SPLIT.md)).

---

## 8. Erros comuns

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| Com `Provider=Asaas` ainda parece Fake | `ApiKey` ou `WebhookSecret` = `CHANGE_ME` / vazio | `ShouldUseAsaas` exige os dois reais |
| Webhook **401** | header ausente ou ≠ `WebhookSecret` | alinhar painel sandbox ↔ env |
| Webhook **400** `TENANT_AUSENTE` | payload sem `externalReference` no formato Prata | cobrança criada pelo adapter |
| Split falha / sem repasse | marketplace pendente ou recebedor `pending-*` | aguardar aprovação; KYC do fotógrafo |
| Sandbox vs prod misturados | BaseUrl de um + chave do outro | um par BaseUrl+ApiKey+wallet por ambiente |
| ApiKey no log/commit | exposição | rotacionar no Asaas **antes** de apagar histórico |

---

## Produção (nota)

Só depois de webhook + conciliação verdes no sandbox ([08 §9](../08-PAGAMENTOS-SPLIT.md)). Conta em [www.asaas.com](https://www.asaas.com), BaseUrl `https://api.asaas.com/v3`, nomes tipo `prata-prod` / `webhook-prata-prod`, e URL do webhook apontando para a API publicada (convenção: `https://api.prata.app/v1/webhooks/asaas` — [deploy-e1](../deploy-e1.md)). Checklist: [deploy-e1.md](../deploy-e1.md); aceite de piloto ainda aberto ([13](../13-ROADMAP-E-RISCOS.md)).

---

## Referências rápidas

| | |
|---|---|
| Código | `AsaasPaymentGateway`, `AsaasOptions`, `WebhookPaymentProcessor` |
| Endpoint | `POST /v1/webhooks/asaas` · header `asaas-access-token` |
| ADR | [ADR-0005](../adr/ADR-0005-psp-asaas-com-port.md) |
| Ambientes | [14](../14-AMBIENTES-E-OPERACAO.md) · [deploy-e1](../deploy-e1.md) |
| Etapa | [E3 · Dinheiro](../etapas/E3-DINHEIRO.md) |
