# ADR-0005 · Asaas atrás do port `IPaymentGateway`

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E3

## Contexto

O produto precisa de **split de pagamento com subcontas de recebimento**: o
cliente paga, o dinheiro cai dividido entre a subconta do fotógrafo e a
subconta da plataforma, e o valor cheio **nunca** passa pela conta da
plataforma.

Receber para repassar depois é atividade de instituição de pagamento, regulada
pelo Banco Central (Circular 3.682/2013 e Resolução BCB 150/2021). Sem
autorização: risco regulatório, o valor cheio virando receita tributável da
plataforma, e bloqueio de conta por movimentação atípica assim que o volume
crescer. Ver [08 · Pagamentos e split](../08-PAGAMENTOS-SPLIT.md).

## Decisão

- **PSP: Asaas** na v1, em sandbox durante toda a E3.
- **Atrás do port `IPaymentGateway`**, em `Application/Abstractions/`. Trocar
  de PSP deve custar uma classe em `Infrastructure/Payments/`.
- O port **não expõe** nada que aceite dado de cartão nem que transfira
  dinheiro entre contas. Método novo com uma dessas características exige ADR
  novo.
- **Pagar.me é o plano B declarado**, atrás do mesmo port.
- Pix é o meio principal; cartão para parcelamento do saldo; boleto opcional.

## Alternativas consideradas

| PSP | Split | Por que não como principal |
|---|---|---|
| **Asaas** | nativo (`walletId`) | **escolhido**: melhor documentação para este caso, cobrança recorrente e régua de inadimplência inclusas |
| Pagar.me | regras avançadas | split mais configurável. **Plano B**; vale se a estrutura de comissão ficar complexa — 2º fotógrafo recebendo direto, por exemplo |
| Mercado Pago | `marketplace_fee` | alcance e confiança do consumidor final. Onboarding do vendedor por OAuth adiciona um fluxo a manter |
| Iugu / Celcoin / Zoop (BaaS) | sim | mais poder e mais responsabilidade regulatória. Só faz sentido em escala |
| Stripe Connect | excelente DX | Pix e boleto locais são o gargalo. Produto brasileiro precisa de Pix de primeira classe |
| Receber na conta da plataforma e repassar | — | **inviável**: é atividade regulada. Ver contexto |
| Open Finance | — | **confusão comum**: Open Finance consulta dados e inicia pagamentos. Não serve para receber em nome de terceiro |

## Consequências

### Boas

- O dinheiro nunca passa pela plataforma: sem risco regulatório e sem o valor
  cheio virando receita tributável.
- O **KYC é do PSP**, não nosso. Não coletamos nem guardamos documento em
  claro.
- PCI-DSS SAQ-A: tokenização do PSP, zero dado de cartão aqui.
- Troca de PSP é uma classe, com o plano B já nomeado.

### Ruins e o que fazemos a respeito

- **Dependência externa bloqueante.** Split com subcontas exige que a
  **plataforma** seja aprovada como marketplace no PSP — análise comercial que
  leva semanas e não depende de código. Mitigação: a tarefa está na **E1**,
  não na E3 ([E1 · Fundação](../etapas/E1-FUNDACAO.md)).
- **A taxa do PSP entra na conta do fotógrafo** junto com a nossa comissão.
  São dois descontos na mesma transação, e ele vai perguntar. O painel
  financeiro precisa mostrar os dois separados
  ([01 · Glossário](../01-GLOSSARIO.md), termos proibidos).
- **KYC do fotógrafo travando repasse** é ticket de suporte garantido.
  Mitigado por deixar isso visível no back-office dele
  ([RN-FIN-030](../06-REGRAS-DE-NEGOCIO.md)) — sem a tela, a conclusão dele é
  que a plataforma está retendo.
- **Webhook é a única fonte de verdade** e o PSP reenvia fora de ordem.
  Mitigado por `payment_event` com `UNIQUE (external_event_id)` e regra de não
  regressão de estado ([RN-FIN-021](../06-REGRAS-DE-NEGOCIO.md),
  [RN-FIN-023](../06-REGRAS-DE-NEGOCIO.md)).
- **Emissão de nota da comissão** é questão contábil ainda aberta. Precisa de
  contador antes de faturar ([13 · Roadmap](../13-ROADMAP-E-RISCOS.md)).
