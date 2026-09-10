# 05 · Máquinas de estado

Enums no `Domain`, transições validadas **dentro da entidade**. Nunca
`order.Status = X` solto num handler. Cada transição publica um evento de
domínio, e cada linha das tabelas abaixo vira um teste de unidade.

O significado de negócio de cada estado está em
[01 · Glossário](01-GLOSSARIO.md), seção 3. Aqui estão as **transições**.

---

## 1. Regras de implementação

| Regra | Motivo |
|---|---|
| Um método por transição, com nome de negócio: `Confirmar()`, `Recusar()`, `Reagendar()` | `SetStatus(status)` aceita qualquer coisa e não documenta nada |
| O método retorna `Result` — regra violada é valor, não exceção | Ver [02 · Arquitetura](02-ARQUITETURA.md), seção 4.1 |
| Transição não listada é erro `TransicaoInvalida`, com estado origem e destino no código do erro | O suporte precisa saber o que o usuário tentou fazer |
| Transição disparada por job carrega o motivo (`Expirado por validade de orçamento`) | Sem isso, ninguém explica ao cliente por que o pedido morreu |
| Todo estado terminal é explícito na tabela | Estado terminal implícito gera pedido zumbi na lista |
| Data/hora entra por `IDateTimeProvider`, nunca `DateTime.Now` | Sem isso não se testa expiração |

**Teste obrigatório por máquina:** para cada estado, tentar **todas** as
transições e afirmar que só as da tabela passam. É um teste parametrizado,
não trinta testes escritos à mão. Ver
[15 · Estratégia de testes](15-ESTRATEGIA-DE-TESTES.md).

---

## 2. Pedido — `OrderStatus`

```
 Rascunho ─► Enviado ─► EmAnalise ─► OrcamentoEnviado ─► Aprovado ─► Confirmado
                                                                        │
                            ┌───────────────────────────────────────────┘
                            ▼
                        Agendado ─► Realizado ─► EmEdicao ─► Entregue ─► Concluido
```

| De | Para | Quem | Guarda | Evento |
|---|---|---|---|---|
| — | `Rascunho` | `client` | serviço existe e está ativo; data pretendida ≥ hoje | `PedidoCriado` |
| `Rascunho` | `Enviado` | `client` | briefing com obrigatórias respondidas ([RN-BRF-010](06-REGRAS-DE-NEGOCIO.md)) | `PedidoEnviado` |
| `Enviado` | `EmAnalise` | `tenant.*` | — | `PedidoEmAnalise` |
| `Enviado` \| `EmAnalise` | `Recusado` | `tenant.*` | motivo obrigatório | `PedidoRecusado` |
| `EmAnalise` | `OrcamentoEnviado` | `tenant.owner` | ≥ 1 item; total > 0; validade definida | `OrcamentoEnviado` |
| `OrcamentoEnviado` | `OrcamentoEnviado` | `tenant.owner` | revisão da proposta; **reinicia a validade** | `OrcamentoRevisado` |
| `OrcamentoEnviado` | `Aprovado` | `client` | dentro da validade | `OrcamentoAprovado` |
| `OrcamentoEnviado` | `Expirado` | **job** | `agora > Quote.ValidoAte` | `PedidoExpirado` |
| `OrcamentoEnviado` \| `Aprovado` | `EmEspera` | `tenant.*` \| `client` | motivo obrigatório | `PedidoEmEspera` |
| `EmEspera` | estado anterior | `tenant.*` | — | `PedidoRetomado` |
| `Aprovado` | `Confirmado` | **sistema** | **sinal `Confirmado` E contrato `Assinado`** ([RN-COM-020](06-REGRAS-DE-NEGOCIO.md)) | `PedidoConfirmado` |
| `Aprovado` \| `Confirmado` | `CanceladoPeloCliente` | `client` | política de reembolso por janela ([RN-COM-040](06-REGRAS-DE-NEGOCIO.md)) | `PedidoCancelado` |
| `Aprovado` \| `Confirmado` \| `Agendado` | `CanceladoPeloEstudio` | `tenant.owner` | motivo obrigatório; reembolso integral por padrão | `PedidoCancelado` |
| `Confirmado` | `Agendado` | `tenant.*` | agenda disponível (**E5**) | `AgendamentoDetalhado` |
| `Confirmado` \| `Agendado` | `Reagendado` | `tenant.owner` \| `client` | nova data livre; mantém pagamentos | `PedidoReagendado` |
| `Reagendado` | `Confirmado` | **sistema** | nova data aceita pelas duas partes | `PedidoConfirmado` |
| `Confirmado` \| `Agendado` | `Realizado` | `tenant.*` | `agora ≥ data do evento` | `PedidoRealizado` |
| `Realizado` | `EmEdicao` | `tenant.*` | — | `EdicaoIniciada` |
| `EmEdicao` | `Entregue` | `tenant.*` | galeria `Disponivel` **e** saldo `Confirmado` ([RN-COM-050](06-REGRAS-DE-NEGOCIO.md)) | `PedidoEntregue` |
| `Entregue` | `Concluido` | **job** \| `tenant.*` | `SelecaoFechada` e download feito, ou prazo de seleção vencido | `PedidoConcluido` |

**Estados terminais:** `Concluido`, `Recusado`, `Expirado`,
`CanceladoPeloCliente`, `CanceladoPeloEstudio`.

### `Confirmado` é o marco

Só entra aqui com **sinal pago + contrato assinado**. É o que reserva a data.
Não existe caminho manual para `Confirmado`: nem o `tenant.owner` pode
forçar — se o cliente pagou por fora, o fluxo é registrar o pagamento
manualmente no Financeiro, e daí o sistema confirma. Isso mantém a trilha de
auditoria honesta.

### A decisão sobre `Agendado`

A spec original punha `Confirmado → Agendado` e, ao mesmo tempo, dizia que
`Confirmado` é o que bloqueia a data. Os dois não podem ser verdade sem
definição — e a Agenda só existe na E5, enquanto `Confirmado` chega na E3.

**Resolução adotada:**

- **`Confirmado` reserva a data.** Sempre. É o marco de negócio e o gatilho
  de `DataReservada`.
- **`Agendado` é detalhe logístico**, não reserva: horário exato, buffer de
  deslocamento, equipe escalada. Só ganha função quando o módulo de Agenda
  existir.
- **Entre a E3 e a E5**, `Confirmado → Realizado` é transição válida e
  `Agendado` não é usado. A tabela acima já permite os dois caminhos.

A alternativa considerada — subir um `Booking` mínimo na E3, apenas com
bloqueio de data, sem disponibilidade nem buffer — está avaliada em
[E3 · Dinheiro](etapas/E3-DINHEIRO.md). Ela custa cerca de dois dias e
elimina o risco de dupla reserva no período em que o produto já cobra mas
ainda não tem agenda. **Recomendação: fazer.**

---

## 3. Pagamento — por meio de pagamento

**Uma máquina genérica de pagamento é errada aqui.** `Autorizado` não existe
em Pix; `Liquidado` é D+0 no Pix e D+30 no cartão. Modelar um caminho só faz
a primeira cobrança Pix violar o próprio diagrama.

### 3.1 Pix

```
Pendente ─► Processando ─► Confirmado ─► Liquidado ─► Repassado
```

| De | Para | Gatilho | Nota |
|---|---|---|---|
| `Pendente` | `Processando` | QR/copia-e-cola gerado, aguardando | TTL do QR (padrão 30 min) |
| `Processando` | `Confirmado` | webhook `PAYMENT_RECEIVED` | segundos |
| `Processando` | `Expirado` | TTL do QR venceu | job ou webhook |
| `Confirmado` | `Liquidado` | webhook de liquidação | D+0 / D+1 |
| `Liquidado` | `Repassado` | split creditado na subconta | **só com KYC `Aprovado`** |
| `Confirmado` \| `Liquidado` | `EstornoSolicitado` → `Estornado` \| `EstornoParcial` | devolução via PSP | Pix tem devolução, não chargeback |

**`Autorizado` e `Chargeback` não existem no caminho Pix.**

### 3.2 Cartão de crédito

```
Pendente ─► Processando ─► Autorizado ─► Confirmado ─► Liquidado ─► Repassado
```

| De | Para | Gatilho | Nota |
|---|---|---|---|
| `Processando` | `Autorizado` | autorização aprovada | dinheiro reservado, não capturado |
| `Processando` | `Recusado` | emissor negou | motivo do PSP guardado |
| `Autorizado` | `Confirmado` | captura | captura automática no fluxo padrão |
| `Autorizado` | `Expirado` | autorização venceu sem captura | ~5 dias |
| `Confirmado` | `Liquidado` | compensação | **D+30**, ou antecipação com taxa |
| `Confirmado` \| `Liquidado` | `Chargeback` → `EmDisputa` | contestação na bandeira | prazo de defesa; ver [08](08-PAGAMENTOS-SPLIT.md) |
| `EmDisputa` | `Liquidado` \| `Estornado` | resultado da disputa | — |

### 3.3 Boleto

```
Pendente ─► Confirmado ─► Liquidado ─► Repassado
```

Sem `Processando` e sem `Autorizado`. `Expirado` no vencimento. D+1/D+2.
Opcional no produto — conversão baixa hoje.

### 3.4 Parcela — `InstallmentStatus`

Cada `Installment` tem a máquina do **seu** meio de pagamento. O `Payment`
agregado deriva o estado das parcelas:

| Estado do `Payment` | Quando |
|---|---|
| `Pendente` | nenhuma parcela `Confirmado` |
| `ParcialmentePago` | ≥ 1 parcela `Confirmado`, mas não todas |
| `Confirmado` | todas as parcelas `Confirmado` |
| `Liquidado` | todas as parcelas `Liquidado` |

`ParcialmentePago` existe no `Payment` e **não** existe na `Installment`.

---

## 4. Repasse — `PayoutStatus`

```
Agendado ─► EmTransito ─► Liquidado
```

| De | Para | Gatilho |
|---|---|---|
| — | `Agendado` | pagamento `Liquidado` **e** KYC `Aprovado` |
| — | `BloqueadoKyc` | pagamento `Liquidado` **e** KYC ≠ `Aprovado` ([RN-FIN-030](06-REGRAS-DE-NEGOCIO.md)) |
| `BloqueadoKyc` | `Agendado` | KYC passou a `Aprovado` — job de reprocessamento |
| `Agendado` | `EmTransito` | PSP iniciou a transferência |
| `EmTransito` | `Liquidado` | confirmação do PSP |
| `EmTransito` | `Falhou` | dado bancário inválido, erro técnico |
| `Falhou` | `Agendado` | após correção do dado, novo agendamento |
| qualquer | `Retido` | disputa, chargeback ou ordem judicial |

`BloqueadoKyc` **tem que aparecer no back-office do fotógrafo**, não só no
log da plataforma. É a diferença entre um ticket de suporte e uma acusação de
retenção indevida.

---

## 5. Contrato — `ContractStatus`

```
Rascunho ─► Enviado ─► Visualizado ─► Assinado
```

| De | Para | Quem | Guarda |
|---|---|---|---|
| — | `Rascunho` | `tenant.owner` | gerado de template do tenant |
| `Rascunho` | `Enviado` | `tenant.owner` | pedido `Aprovado`; PDF gerado e com hash calculado |
| `Enviado` | `Visualizado` | `client` | primeira abertura registra IP e user-agent |
| `Enviado` \| `Visualizado` | `Assinado` | `client` | aceite explícito; grava hash + IP + UA + timestamp |
| `Enviado` \| `Visualizado` | `Recusado` | `client` | motivo opcional |
| `Enviado` \| `Visualizado` | `Expirado` | **job** | validade vencida |
| `Rascunho` \| `Enviado` | `Cancelado` | `tenant.owner` | — |

**`Assinado` é terminal e imutável.** Correção depois de assinado gera um
contrato novo, com referência ao anterior — nunca edita o assinado.

---

## 6. Galeria — `GalleryStatus`

```
EmPreparo ─► Disponivel ─► EmSelecao ─► SelecaoFechada ─► Entregue
                  ▲              │
                  └── BloqueadaPorPendencia ──┘
```

| De | Para | Quem | Guarda |
|---|---|---|---|
| — | `EmPreparo` | **sistema** | reage a `PedidoRealizado` |
| `EmPreparo` | `Disponivel` | `tenant.*` | todas as derivadas geradas ([RN-ENT-010](06-REGRAS-DE-NEGOCIO.md)) |
| `Disponivel` | `BloqueadaPorPendencia` | **sistema/job** | existe parcela vencida não paga |
| `BloqueadaPorPendencia` | `Disponivel` | **sistema** | reage a `PagamentoConfirmado` |
| `Disponivel` | `EmSelecao` | `client` \| `tenant.*` | limite de favoritas definido pelo pacote |
| `EmSelecao` | `SelecaoFechada` | `client` | seleção ≤ limite, ou excedente pago ([RN-ENT-020](06-REGRAS-DE-NEGOCIO.md)) |
| `EmSelecao` | `SelecaoFechada` | **job** | prazo de seleção vencido — fecha com o que houver |
| `SelecaoFechada` | `Entregue` | `tenant.*` | saldo `Confirmado`; libera original ([RN-ENT-021](06-REGRAS-DE-NEGOCIO.md)) |
| `Disponivel`…`Entregue` | `Expirada` | **job** | passou o prazo contratual |
| `Expirada` | `Arquivada` | **job** | move para storage frio |
| `Arquivada` | `Disponivel` | `tenant.owner` | reativação sob demanda, com custo |

### `BloqueadaPorPendencia` — a regra que economiza suporte

Se o saldo não foi pago, a galeria **abre**, em baixa resolução e com marca
d'água, e o download em alta fica travado. O cliente vê as fotos e vê por que
não pode baixar.

É a alavanca de cobrança mais eficaz do produto e evita a conversa
constrangedora por WhatsApp. **Não** é o mesmo que esconder a galeria:
esconder gera ligação de reclamação; mostrar com marca d'água gera pagamento.

---

## 7. KYC do fotógrafo — `KycStatus`

```
NaoIniciado ─► EmAnalise ─► Aprovado
```

| De | Para | Gatilho |
|---|---|---|
| `NaoIniciado` | `EmAnalise` | fotógrafo enviou documentos ao PSP |
| `EmAnalise` | `PendenteDocumento` | PSP pediu complemento |
| `PendenteDocumento` | `EmAnalise` | reenvio |
| `EmAnalise` | `Aprovado` | PSP aprovou → dispara reprocessamento de `Payout` em `BloqueadoKyc` |
| `EmAnalise` | `Reprovado` | PSP negou |
| `Aprovado` | `Suspenso` | PSP suspendeu o recebedor |

O status vem **do PSP por webhook**, nunca de digitação no back-office.
Ver [08 · Pagamentos e split](08-PAGAMENTOS-SPLIT.md).

---

## 8. A coreografia da confirmação

O ponto de maior acoplamento do sistema, e o que mais confunde. Nenhum
módulo chama o outro: todos reagem a evento.

```
 client aprova orçamento
   │
   └─► Comercial: Order → Aprovado  ── OrcamentoAprovado ──┐
                                                            ▼
                                    Financeiro: cria Payment do sinal com split
                                                            │
                                            ┌───────────────┴──────────────┐
                                            ▼                              ▼
                          Contrato: Rascunho → Enviado         cliente paga o sinal
                                            │                              │
                                            ▼                              ▼
                              client assina → Assinado        webhook → Confirmado
                                            │                              │
                                            └──────────┬───────────────────┘
                                                       ▼
                                    Comercial reavalia:  sinal Confirmado?  E
                                                         contrato Assinado?
                                                       ▼
                                          Order → Confirmado ── PedidoConfirmado ──┐
                                                                                    ▼
                                                          Agenda: DataReservada (E5)
                                                          Notificações: e-mail + wa.me
```

Detalhes que importam:

- A reavaliação de `Aprovado → Confirmado` é **idempotente** e disparada por
  **dois** eventos diferentes (`PagamentoConfirmado` e `ContratoAssinado`).
  Qualquer ordem de chegada funciona, e a chegada em duplicata não confirma
  duas vezes.
- O webhook do PSP pode chegar **antes** de o navegador do cliente voltar do
  checkout. O estado nunca depende do retorno do navegador
  ([RN-FIN-022](06-REGRAS-DE-NEGOCIO.md)).
- Se o cliente pagar o sinal e nunca assinar, o pedido fica em `Aprovado` com
  dinheiro `Confirmado`. Isso é um **estado esperado**, precisa aparecer no
  painel do fotógrafo como pendência acionável, e o job de lembrete cobra a
  assinatura. Ver [RN-CTR-020](06-REGRAS-DE-NEGOCIO.md).
