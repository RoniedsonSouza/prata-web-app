# E3 · Dinheiro

**Marco:** a plataforma passa a faturar.
**Ordem de grandeza:** 5 a 7 semanas, **mais** a espera de habilitação no PSP
— que corre em paralelo desde a [E1](E1-FUNDACAO.md).
**Depende de:** E1, E2.

> **Esqueleto de propósito.** Objetivo, critério de aceite e riscos estão
> fechados; a lista de tarefas se detalha quando a etapa começar. Detalhar
> tarefa a três meses de distância, com dois pilotos ainda dando feedback, é
> escrever ficção. A referência técnica completa já existe em
> [08 · Pagamentos e split](../08-PAGAMENTOS-SPLIT.md).

---

## 1. Objetivo

Cobrar sinal e saldo com split, repassar ao fotógrafo, conciliar com o PSP e
mostrar os três estados do dinheiro separados no painel do estúdio.

## 2. Escopo

| Entra | Não entra |
|---|---|
| Adapter do Asaas atrás de `IPaymentGateway` | galeria (E4) |
| Onboarding do recebedor + KYC | contrato e assinatura (E5) |
| `PayoutAccount`, `SplitRule` versionada | agenda completa (E5) |
| `Payment` + `Installment`, sinal e saldo | antecipação de recebível |
| Webhook verificado e idempotente | nota fiscal |
| Máquinas de pagamento por meio (Pix, cartão, boleto) | assinatura mensal de tenant |
| `Payout` e bloqueio por KYC | |
| Job de conciliação diária | |
| Painel financeiro do estúdio | |
| Estorno e política de cancelamento | |
| Chargeback e disputa | |
| Estados `Confirmado` → `Realizado` do pedido | |

## 3. Decisão a fechar no início da etapa

**Subir um `Booking` mínimo já aqui?** Só bloqueio de data, sem
disponibilidade nem buffer de deslocamento.

| | |
|---|---|
| Custo | ~2 dias |
| Ganho | elimina o risco de dupla reserva no período em que o produto **já cobra** e ainda não tem agenda (E3 → E5) |
| Alternativa | manter a checagem no Comercial ([RN-AGD-030](../06-REGRAS-DE-NEGOCIO.md)), sujeita a condição de corrida em confirmações simultâneas |
| **Recomendação** | **fazer.** Com o `EXCLUDE` do Postgres de [11](../11-MODELO-DE-DADOS.md), seção 5, o banco garante o que a aplicação não garante |

## 4. Critério de aceite

> Código na branch `feat/e5-agenda` (Fake + `AsaasPaymentGateway`). Aceite
> com dinheiro real / marketplace Asaas **aberto**.

- [ ] Sinal cobrado com split, **liquidado e repassado em produção**
- [x] Webhook idempotente no código: mesmo `external_event_id` não reprocessa *(unit/integração; sandbox ainda pendente)*
- [x] Webhook fora de ordem não regride estado *(domínio)*
- [x] Webhook com assinatura forjada → `401`, nada gravado *(Fake + Asaas parser)*
- [x] Conciliação detecta divergência semeada *(código; job em ambiente publicado pendente)*
- [x] Nenhuma divergência resolvida automaticamente
- [x] KYC pendente **visível no back-office do fotógrafo** *(API studio)*
- [x] Painel financeiro com as três colunas separadas: a receber, liquidado,
      repassado
- [x] Alterar comissão não reescreve cobrança antiga
- [x] Nenhum dado de cartão em lugar nenhum — verificado por teste de contrato
- [ ] **Primeira comissão recebida na conta da plataforma**

## 5. Riscos da etapa

| Risco | Mitigação |
|---|---|
| **Habilitação de split negada ou demorada** | solicitada na E1. Plano B: Pagar.me atrás do mesmo port |
| Webhook perdido ou fora de ordem | `payment_event` com `UNIQUE`, não regressão de estado, e conciliação diária como rede |
| KYC travando repasse e virando acusação de retenção | situação visível no back-office, com o motivo do PSP |
| Estado de pagamento mudando por retorno de navegador | proibido por [RN-FIN-022](../06-REGRAS-DE-NEGOCIO.md); a tela de retorno faz polling |
| Arredondamento de parcela e de comissão | teste de divisão e de percentual desde o primeiro commit |
| Cobrança duplicada por duplo clique | `Idempotency-Key` obrigatório |
| Nota fiscal da comissão sem definição contábil | **resolver com contador antes de faturar** |

## 6. Referências

[08 · Pagamentos e split](../08-PAGAMENTOS-SPLIT.md) ·
[05 · Máquinas de estado](../05-MAQUINAS-DE-ESTADO.md), seção 3 ·
[06 · Regras](../06-REGRAS-DE-NEGOCIO.md), prefixo `FIN` ·
[ADR-0003](../adr/ADR-0003-modelo-de-receita-comissao.md) ·
[ADR-0005](../adr/ADR-0005-psp-asaas-com-port.md)
