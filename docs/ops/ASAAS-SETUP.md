# Configurar Asaas no Prata

Checklist operacional. Decisão de produto: [ADR-0005](../adr/ADR-0005-psp-asaas-com-port.md). Modelo de split: [08](../08-PAGAMENTOS-SPLIT.md).

O Prata sobe `AsaasPaymentGateway` só se `Payments:Provider=Asaas` **e** `ApiKey` + `WebhookSecret` forem valores reais (não `CHANGE_ME`). Caso contrário usa `FakePaymentGateway` — ver `AddPrataPaymentGateway` / `ShouldUseAsaas`.

---

## 1. Conta sandbox vs produção

| Ambiente | URL da API | Conta |
|---|---|---|
| Local / preview / E3 | `https://api-sandbox.asaas.com/v3` | [sandbox.asaas.com](https://sandbox.asaas.com) |
| Produção (piloto+) | `https://api.asaas.com/v3` | [www.asaas.com](https://www.asaas.com) |

1. Crie a conta **da plataforma** (CNPJ/CPF da empresa Prata), não a do fotógrafo.
2. Comece sempre no **sandbox**. Produção só depois de webhook + conciliação verdes ([08 §9](../08-PAGAMENTOS-SPLIT.md)).
3. Sandbox e produção têm **ApiKey e wallets diferentes** — não misture.

---

## 2. ApiKey e token do webhook

No painel Asaas (conta da plataforma):

1. **Integrações → API Key** → gerar chave. Copie uma vez; não commitar.
2. **Integrações → Webhooks** (ou autenticação de webhook) → defina um **token de acesso** (segredo longo, aleatório).
   - Esse valor é o que o Asaas envia no header `asaas-access-token`.
   - No Prata ele vai em `Payments__Asaas__WebhookSecret` (RN-FIN-020).

Nunca use a ApiKey como token do webhook.

---

## 3. Marketplace / split

Split com subcontas exige habilitação **marketplace** na conta da plataforma. Análise comercial do PSP — leva dias/semanas ([E1](../etapas/E1-FUNDACAO.md) item 1).

1. No painel (ou suporte Asaas), solicite **marketplace / split de pagamento**.
2. Enquanto **pendente**:
   - Código e testes seguem com `FakePaymentGateway` ou sandbox sem split real.
   - Não marque aceite de produção / primeira comissão ([E3](../etapas/E3-DINHEIRO.md), [13](../13-ROADMAP-E-RISCOS.md)).
   - Plano B declarado: Pagar.me atrás do mesmo `IPaymentGateway` (ADR-0005).
3. Quando **aprovado**, confirme no painel que dá para criar subcontas (`walletId`) e cobranças com `split`.

---

## 4. Wallet da plataforma (`WalletIdPlatform`)

1. Na conta Asaas da plataforma, obtenha o **walletId** da própria conta (dados da conta / API de conta).
2. Coloque em `Payments__Asaas__WalletIdPlatform`.
3. Esse id identifica a wallet da **comissão** da plataforma. Não é o wallet do fotógrafo.
4. Na cobrança, o adapter envia `split` com `walletId` do **recebedor** (fotógrafo) e valor fixo da parte dele; o restante permanece na conta que emitiu a cobrança (plataforma).

Fotógrafo: onboarding via port `CriarRecebedor` → guarda `walletId` em `PayoutAccount` (não configure wallet de tenant no `.env`).

---

## 5. Variáveis de ambiente

No `.env` (local) ou cofre (deploy). Placeholders só com `CHANGE_ME` — nunca secrets reais no git.

```bash
# Liga o adapter Asaas (sem chaves reais → Fake)
Payments__Provider=Asaas

Payments__Asaas__BaseUrl=https://api-sandbox.asaas.com/v3
# produção: https://api.asaas.com/v3

Payments__Asaas__ApiKey=CHANGE_ME
Payments__Asaas__WebhookSecret=CHANGE_ME
Payments__Asaas__WalletIdPlatform=CHANGE_ME
```

Equivalente em `appsettings` / config .NET: `Payments:Provider`, `Payments:Asaas:BaseUrl`, `Payments:Asaas:ApiKey`, etc.

| Variável | Obrigatória para Asaas real? | Notas |
|---|---|---|
| `Payments__Provider` | sim (`Asaas`) | `Fake` força fake |
| `Payments__Asaas__BaseUrl` | recomendada | default sandbox se vazia |
| `Payments__Asaas__ApiKey` | sim | header HTTP `access_token` nas calls |
| `Payments__Asaas__WebhookSecret` | sim | deve bater com `asaas-access-token` |
| `Payments__Asaas__WalletIdPlatform` | sim em operação | wallet da comissão |

Referência completa: [`.env.example`](../../.env.example) · tabela em [deploy-e1.md](../deploy-e1.md).

---

## 6. Webhook na URL pública

Endpoint do Prata: **`POST /v1/webhooks/asaas`** (anônimo; autenticação = header).

| Onde | URL no painel Asaas |
|---|---|
| Local | túnel → `https://<tunel>/v1/webhooks/asaas` |
| Produção | `https://api.prata.app/v1/webhooks/asaas` |

```bash
# exemplo local — ver docs/14-AMBIENTES-E-OPERACAO.md
cloudflared tunnel --url http://localhost:5080
# cadastre a URL gerada + /v1/webhooks/asaas
```

No painel Asaas:

1. URL = pública HTTPS + path acima.
2. Token de autenticação = mesmo valor de `Payments__Asaas__WebhookSecret`.
3. Eventos mínimos úteis: pagamento confirmado/recebido, estorno, chargeback; KYC/conta se disponíveis no sandbox.
4. Header enviado pelo Asaas: `asaas-access-token: <WebhookSecret>`.

Token errado → API responde **401** `WEBHOOK_ASSINATURA_INVALIDA`, nada gravado.

---

## 7. Validar

### A. Só Fake (dev sem Asaas)

- Deixe `ApiKey`/`WebhookSecret` como `CHANGE_ME` **ou** `Provider=Fake`.
- Webhook fake espera header `asaas-access-token: sandbox-ok` e body no formato interno do fake (não é JSON Asaas).
- Testes: `FakePaymentGatewayTests`, suíte de billing.

### B. Adapter Asaas ligado (sandbox)

1. Preencha ApiKey + WebhookSecret reais do **sandbox** (não `CHANGE_ME`).
2. Reinicie API; confirme nos logs/DI que não está no Fake (chamadas HTTP vão para `api-sandbox`).
3. Smoke **seguro**:
   - Crie cobrança Pix de valor baixo no fluxo do produto (ou painel sandbox).
   - Pague no sandbox Asaas.
   - Confirme webhook `200` e registro em `payment_event` (idempotência: reenvio → `duplicate`).
   - Repita o mesmo evento → uma transição só (RN-FIN-021).
4. Assinatura forjada: `curl` com token errado → **401**.
5. **Não** bata API real do PSP no CI ([08 §9](../08-PAGAMENTOS-SPLIT.md)).

### C. Antes do piloto com dinheiro real

Checklist em [deploy-e1.md](../deploy-e1.md) (marketplace aprovado, secrets no cofre, webhook sandbox ok). Aceite de produção continua aberto até liquidação/repasse reais ([13](../13-ROADMAP-E-RISCOS.md)).

---

## 8. Erros comuns

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| Com `Provider=Asaas` ainda parece Fake | `ApiKey` ou `WebhookSecret` = `CHANGE_ME` / vazio | `ShouldUseAsaas` exige os dois reais |
| Webhook **401** | header ausente ou ≠ `WebhookSecret` | alinhar painel Asaas ↔ env |
| Webhook **400** `TENANT_AUSENTE` | payload sem `externalReference` no formato Prata | cobrança precisa ter sido criada pelo adapter |
| Split falha / sem repasse | marketplace não habilitado ou recebedor `pending-*` | aguardar aprovação; completar KYC do fotógrafo |
| Sandbox vs prod misturados | BaseUrl de um ambiente + chave do outro | um par BaseUrl+ApiKey+wallet por ambiente |
| ApiKey no log/commit | exposição | rotacionar no Asaas **antes** de apagar histórico |

---

## Referências rápidas

| | |
|---|---|
| Código | `AsaasPaymentGateway`, `AsaasOptions`, `AddPrataPaymentGateway` |
| Endpoint | `POST /v1/webhooks/asaas` |
| ADR | [ADR-0005](../adr/ADR-0005-psp-asaas-com-port.md) |
| Ambientes | [14](../14-AMBIENTES-E-OPERACAO.md) · [deploy-e1](../deploy-e1.md) |
| Etapa | [E3 · Dinheiro](../etapas/E3-DINHEIRO.md) |
