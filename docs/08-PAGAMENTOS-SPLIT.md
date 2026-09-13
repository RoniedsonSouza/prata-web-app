# 08 · Pagamentos, split e repasse

O módulo mais sensível do produto. Erro aqui não é bug: é dinheiro do cliente
de outra pessoa.

> **Não é parecer jurídico.** As referências regulatórias abaixo orientam a
> arquitetura. Antes de faturar, o enquadramento precisa ser confirmado com
> contador e com o jurídico do PSP contratado — inclusive o modelo de emissão
> de nota da comissão.

---

## 1. O que **não** se pode fazer

Receber o valor cheio na conta da plataforma e transferir depois para o
fotógrafo é **atividade de instituição de pagamento**, regulada pelo Banco
Central (Circular 3.682/2013 e Resolução BCB 150/2021).

Sem autorização, três problemas, todos reais:

| Problema | Consequência prática |
|---|---|
| Risco regulatório | operar arranjo de pagamento sem autorização |
| Tributário | o valor cheio vira receita tributável da plataforma, não a comissão. Um casamento de R$ 8.000 entra como R$ 8.000 de receita, não como R$ 640 de comissão |
| Operacional | a conta é bloqueada por movimentação atípica assim que o volume crescer — e o bloqueio acontece com dinheiro de casamento dentro |

Isso está no [escopo negativo](00-VISAO-E-ESCOPO.md): **o Prata nunca recebe
para repassar.**

## 2. O caminho correto: split com subcontas

Contrata-se um PSP que ofereça **contas de recebimento (subcontas) + split
nativo**. O dinheiro nunca passa pela conta da plataforma: cai já dividido.

```
                          ┌─────────────────────────────────┐
  cliente paga R$ 8.000   │            PSP                  │
  ──────────────────────► │  split aplicado na liquidação   │
                          └───────┬─────────────────┬───────┘
                                  │                 │
                     R$ 7.360 ────┘                 └──── R$ 640
                          ▼                               ▼
              subconta do fotógrafo            subconta da plataforma
              (walletId, KYC próprio)          (comissão)
```

Fluxo de implementação:

| # | Passo | Quem faz |
|---|---|---|
| 1 | Fotógrafo faz onboarding: CPF/CNPJ, documentos, dados bancários ou chave Pix | fotógrafo, no back-office, contra a API do PSP |
| 2 | **O PSP faz o KYC.** A plataforma não coleta nem valida documento | PSP |
| 3 | O PSP devolve um identificador de recebedor (`walletId` no Asaas, `recipient_id` no Pagar.me) | PSP |
| 4 | A plataforma guarda o identificador em `PayoutAccount` — e nada mais | Prata |
| 5 | Ao gerar a cobrança, envia-se a **regra de split**: X% ou R$ fixo para a plataforma, o resto para o recebedor | Prata |
| 6 | O dinheiro cai dividido. A plataforma recebe só a comissão | PSP |

A tela de "selecionar banco e informar a conta" existe: é o **cadastro da
conta de repasse**, validado pelo PSP.

> **Não confundir com Open Finance.** Open Finance serve para **consultar
> dados** e **iniciar pagamentos** — não para receber em nome de terceiro.
> Recebimento em nome de terceiro é conta de pagamento, e isso é o PSP quem
> faz.

## 3. Escolha do PSP

| PSP | Split / subconta | Pix | Observação |
|---|---|---|---|
| **Asaas** | sim, nativo (`walletId`) | sim | Melhor documentação para este caso; cobrança recorrente e régua de inadimplência inclusas. **Ponto de partida** |
| Pagar.me | sim, regras avançadas | sim | Split mais configurável. Vale se a estrutura de comissão ficar complexa — 2º fotógrafo recebendo direto, por exemplo |
| Mercado Pago | sim, via `marketplace_fee` | sim | Alcance e confiança do consumidor final; onboarding do vendedor por OAuth |
| Iugu / Celcoin / Zoop | sim (BaaS) | sim | Mais poder e mais responsabilidade regulatória. Só em escala |
| Stripe Connect | sim, DX excelente | parcial no BR | Ótimo produto, mas Pix e boleto locais são o gargalo. Evitar como principal aqui |

**Decisão: Asaas na v1**, atrás do port `IPaymentGateway`. Trocar de PSP deve
custar uma classe em `Infrastructure/Payments/`. Ver
[ADR-0005](adr/ADR-0005-psp-asaas-com-port.md).

### Dependência externa bloqueante

Split com subcontas exige que **a plataforma** seja aprovada como marketplace
no PSP — não basta o fotógrafo passar no KYC. Isso é análise comercial e de
compliance do PSP, leva semanas e **não depende de código**.

Por isso a tarefa "abrir conta e solicitar habilitação de split" está na
**E1**, não na E3. Descobrir isso no dia em que a E3 começa custa um mês de
espera com o produto pronto. Ver [E1 · Fundação](etapas/E1-FUNDACAO.md).

## 4. Modelo de cobrança

Padrão do setor: **sinal de 30–50% na confirmação** (é ele que reserva a data)
**+ saldo até N dias antes do evento**, parcelável.

Modelagem: um `Payment` com N `Installment`, cada parcela com sua própria
máquina de estados ([05](05-MAQUINAS-DE-ESTADO.md), seção 3).

| Meio | Compensação | Uso sugerido |
|---|---|---|
| **Pix** | instantâneo (D+0/D+1 no split) | **Sinal e saldo. Meio principal** — menor taxa, menor risco de chargeback |
| Cartão de crédito | D+30, ou antecipação com taxa | Parcelamento do saldo até a data do evento |
| Boleto | D+1 / D+2 | Opcional. Conversão baixa hoje |

Regras associadas: sinal na faixa 30–50%
([RN-FIN-010](06-REGRAS-DE-NEGOCIO.md)), saldo N dias antes
([RN-FIN-011](06-REGRAS-DE-NEGOCIO.md)), soma das parcelas exata com
arredondamento na última ([RN-FIN-004](06-REGRAS-DE-NEGOCIO.md)).

**Priorizar Pix no sinal não é só economia de taxa:** Pix não tem chargeback.
O sinal é justamente o valor que o cliente contesta quando desiste do evento.

## 5. Comissão da plataforma

| Regra | Detalhe |
|---|---|
| Configurável por tenant | `SplitRule` com percentual e/ou valor fixo |
| **Versionada** | `VigenteDe` / `VigenteAte`. Nunca `UPDATE` destrutivo ([RN-FIN-033](06-REGRAS-DE-NEGOCIO.md)) |
| **Snapshot na cobrança** | a regra aplicada é gravada no `Payment`. Mudar a taxa hoje não reescreve cobrança de ontem ([RN-FIN-032](06-REGRAS-DE-NEGOCIO.md)) |
| Padrão | 8% (`Payments__DefaultPlatformFeePercent`), ajustável por negociação |

A separação entre "regra vigente" e "regra aplicada" é o que permite negociar
uma taxa menor com um tenant específico sem gerar divergência de conciliação
nas cobranças antigas.

## 6. Não negociável na implementação

Sete itens. Nenhum é opinião.

### 6.1 Nunca armazenar dado de cartão

Checkout transparente com **tokenização do PSP**. Nem PAN, nem CVV, nem
validade — em nenhum log, nenhuma tabela, nenhum trace. Isso mantém a
plataforma em **PCI-DSS SAQ-A**, o nível mais leve.
[RN-FIN-002](06-REGRAS-DE-NEGOCIO.md).

### 6.2 Webhook verificado

Assinatura verificada **antes** de qualquer processamento. Assinatura inválida
→ `401`, e **não** grava evento. Sem isso, qualquer um marca uma cobrança
como paga com um `curl`. [RN-FIN-020](06-REGRAS-DE-NEGOCIO.md).

### 6.3 Webhook idempotente

```sql
CREATE TABLE payment_event (
  id                 uuid PRIMARY KEY,
  tenant_id          uuid NOT NULL,
  payment_id         uuid,
  external_event_id  text NOT NULL,
  event_type         text NOT NULL,
  payload            jsonb NOT NULL,
  received_at        timestamptz NOT NULL,
  processed_at       timestamptz,
  process_error      text,
  CONSTRAINT uq_payment_event_external UNIQUE (external_event_id)
);
```

O PSP **reenvia**, e reenvia **fora de ordem**. Duas defesas:

- `UNIQUE (external_event_id)`: reenvio é aceito com `200` e ignorado
  ([RN-FIN-021](06-REGRAS-DE-NEGOCIO.md)).
- Evento mais antigo que o estado atual é gravado e descartado — nunca
  regride estado ([RN-FIN-023](06-REGRAS-DE-NEGOCIO.md)).

O endpoint responde `200` rápido e processa em background. PSP que recebe
timeout reenfileira e multiplica o problema.

### 6.4 Nunca confiar no retorno do navegador

O estado do pagamento muda **somente** por webhook verificado ou por consulta
ativa à API do PSP. A URL de retorno do checkout serve para mostrar tela ao
cliente — nunca para alterar estado.
[RN-FIN-022](06-REGRAS-DE-NEGOCIO.md).

Corolário de UX: a tela de retorno mostra "estamos confirmando seu pagamento"
e faz polling, em vez de afirmar "pagamento aprovado".

### 6.5 Job diário de conciliação

Compara `Payment` local com o extrato do PSP e abre
`DivergenciaDeConciliacao` para cada diferença de valor ou de estado
([RN-FIN-050](06-REGRAS-DE-NEGOCIO.md)).

**Nenhuma divergência se resolve automaticamente.** Toda baixa é manual, com
registro de quem fez ([RN-FIN-051](06-REGRAS-DE-NEGOCIO.md)) — conciliação
que se auto-resolve esconde exatamente o bug que ela existe para achar.

Alarme: divergência aberta há mais de 24 h é incidente, não relatório. Ver
[14 · Ambientes e operação](14-AMBIENTES-E-OPERACAO.md).

### 6.6 Repasse bloqueado por KYC — e visível

Repasse bloqueado enquanto o KYC não estiver `Aprovado`, **e isso aparece no
back-office do fotógrafo**, não só no log da plataforma
([RN-FIN-030](06-REGRAS-DE-NEGOCIO.md)).

A diferença é concreta: sem a tela, o fotógrafo vê o cliente pagando e o
dinheiro não chegando, e a conclusão dele é que a plataforma está retendo.
Com a tela, ele vê "documento pendente no PSP" e resolve sozinho.

Aprovação de KYC dispara reprocessamento automático dos repasses retidos
([RN-FIN-031](06-REGRAS-DE-NEGOCIO.md)).

### 6.7 Os três estados do dinheiro, separados

`Confirmado` ≠ `Liquidado` ≠ `Repassado`. Ver
[01 · Glossário](01-GLOSSARIO.md), seção 2. O painel financeiro mostra três
colunas, nunca um número chamado "faturamento".

## 7. Chargeback

| Passo | Ação |
|---|---|
| Webhook de contestação | pagamento → `Chargeback` → `EmDisputa` |
| Repasse correspondente | retido imediatamente (`Retido`) |
| `tenant.owner` | notificado, com prazo de defesa visível |
| Provas montadas pelo sistema | contrato assinado com hash + IP + timestamp, log de aceite, registro de entrega da galeria, histórico de acesso do cliente |
| Resultado | volta a `Liquidado` ou vai para `Estornado` |

[RN-FIN-041](06-REGRAS-DE-NEGOCIO.md).

A mitigação real do risco é anterior à disputa: **contrato assinado + log de
aceite + entrega registrada + Pix no sinal.** Ver
[13 · Roadmap e riscos](13-ROADMAP-E-RISCOS.md).

## 8. Contrato do port

```csharp
// Application/Abstractions/IPaymentGateway.cs
public interface IPaymentGateway
{
    // Onboarding do recebedor. O KYC é do PSP; guardamos só o identificador.
    Task<Result<PayoutAccountRef>> CriarRecebedor(RecebedorRequest req, CancellationToken ct);
    Task<Result<KycStatus>>        ConsultarKyc(PayoutAccountRef reference, CancellationToken ct);

    // Cobrança com split. Sem SplitRule, falha: RN-FIN-001.
    Task<Result<ChargeRef>>   CriarCobranca(ChargeRequest req, CancellationToken ct);
    Task<Result<ChargeState>> ConsultarCobranca(ChargeRef reference, CancellationToken ct);
    Task<Result<Unit>>        CancelarCobranca(ChargeRef reference, CancellationToken ct);
    Task<Result<RefundRef>>   Estornar(ChargeRef reference, Money? parcial, CancellationToken ct);

    // Webhook: verificação de assinatura e tradução para evento de domínio.
    Result<WebhookEnvelope> VerificarEAnalisar(string rawBody, IReadOnlyDictionary<string, string> headers);

    // Conciliação.
    Task<Result<IReadOnlyList<SettlementLine>>> ObterExtrato(DateOnly de, DateOnly ate, CancellationToken ct);
}
```

O que **não** entra nesse port, de propósito: nada que aceite dado de cartão,
e nada que transfira dinheiro entre contas. Se um método novo precisar de um
desses, a decisão de arquitetura mudou e precisa de ADR.

## 9. Sandbox e testes

| Item | Como |
|---|---|
| Ambiente | `api-sandbox.asaas.com` na E3 inteira. Produção só com conciliação verde. Setup: [ops/ASAAS-SETUP.md](ops/ASAAS-SETUP.md) |
| Adapter | `AsaasPaymentGateway` em `Infrastructure/Payments/` quando `Payments:Provider=Asaas` + chaves; senão `FakePaymentGateway` |
| Webhook | `POST /v1/webhooks/asaas` · header `asaas-access-token` |
| Webhook local | túnel (`cloudflared` / `ngrok`) apontando para a API local |
| Teste de integração | adapter do PSP com HTTP falso, **mais** um conjunto de payloads reais de webhook salvos como fixture |
| Payloads a cobrir | confirmação, liquidação, reenvio duplicado, chegada fora de ordem, estorno, chargeback, KYC aprovado, KYC reprovado |
| Nunca | teste automatizado batendo na API real do PSP em CI |

Ver [15 · Estratégia de testes](15-ESTRATEGIA-DE-TESTES.md).
