# 01 · Glossário e linguagem ubíqua

Um termo, um significado, em todo lugar: conversa, documento, código, tela e
coluna de banco. Quando o fotógrafo diz "sinal", o código diz `Deposit` e a
tela diz "sinal" — e nunca "entrada", "reserva" ou "adiantamento".

**Convenção de idioma:** identificadores em inglês; **membros de enum de
estado de negócio em português**, porque estado é a palavra que o fotógrafo
usa ao telefone. Ver [ADR-0009](adr/ADR-0009-idioma-do-codigo.md).

---

## 1. Termos do domínio

| Português (produto, tela, conversa) | Código | Significado exato |
|---|---|---|
| Estúdio / tenant | `Tenant` | Uma conta de fotógrafo na plataforma. Unidade de isolamento de dados |
| Fotógrafo (dono) | `User` com role `tenant.owner` | Único que vê financeiro e conta de repasse |
| Equipe | `User` com role `tenant.staff` | 2º fotógrafo, editor. Sem financeiro |
| Cliente | `Client` | Quem contrata. Pertence ao tenant, não à plataforma |
| Convidado | — (sessão anônima) | Quem abre galeria por link + senha. Não tem conta |
| Visitante | — (`anon`) | Quem navega o portfólio público |
| Tipo de serviço | `ServiceType` | Casamento, pré-wedding, gestante, newborn… |
| Pacote | `Package` | Combinação de entregáveis, horas, nº de fotos e preço |
| Adicional | `Addon` | Drone, 2º fotógrafo, hora extra, álbum |
| Pedido | `Order` | Agregado central. Nasce do cliente, atravessa todo o ciclo |
| Orçamento | `Quote` | Proposta com validade. Snapshot de preço dentro do pedido |
| Briefing | `Briefing` / `Answer` | Respostas do cliente às perguntas do serviço |
| Ficha de direção | `ShootSheet` | PDF de uma página que o fotógrafo leva no dia |
| Coleção | `Collection` | Agrupamento curado no portfólio público |
| Galeria | `Gallery` | Álbum privado de entrega de um pedido |
| Derivada | `PhotoVariant` | Versão processada da foto (thumb, web, textura, LQIP) |
| Original | `PhotoVariant` tipo `Original` | Arquivo como saiu da câmera/edição. Nunca alterado |
| Seleção | `Selection` | Escolha de favoritas pelo cliente, com limite do pacote |
| Link de compartilhamento | `ShareLink` | Token assinado + senha + expiração |
| Sinal | `Installment` tipo `Deposit` | Parcela que **reserva a data**. 30–50% |
| Saldo | `Installment` tipo `Balance` | O que falta, vence antes do evento |
| Cobrança | `Payment` | Uma intenção de cobrança no PSP, com 1..N parcelas |
| Parcela | `Installment` | Uma cobrança individual, com máquina de estados própria |
| Conta de repasse | `PayoutAccount` | Subconta do fotógrafo no PSP. Guarda o `walletId` |
| Regra de split | `SplitRule` | Percentual da plataforma. Versionada, com histórico |
| Repasse | `Payout` | Crédito do split na subconta do fotógrafo |
| Comissão | `PlatformFee` | A parte da plataforma dentro do split |
| Contrato | `Contract` | Documento aceito pelo cliente |
| Assinatura | `Signature` | Registro do aceite: hash, IP, user-agent, timestamp |
| Bloqueio de data | `BlackoutDate` | Data indisponível por escolha do estúdio |
| Reserva de data | `Booking` | Data ocupada por pedido confirmado |
| Trilha de auditoria | `AuditLog` | Registro imutável de operação sobre dinheiro ou dado pessoal |

## 2. Os três estados do dinheiro

A confusão mais cara do produto. **Separe sempre.** É o que permite mostrar
ao fotógrafo "pago, mas ainda não caiu" em vez de gerar uma ligação de
suporte.

| Estado | Significado | Quando ocorre |
|---|---|---|
| `Confirmado` | O PSP reconheceu que o cliente pagou | Pix: segundos. Cartão: na autorização + captura |
| `Liquidado` | O dinheiro compensou de fato no PSP | Pix: D+0. Cartão: D+30. Boleto: D+1/D+2 |
| `Repassado` | O split creditou na subconta do fotógrafo | Depois de `Liquidado`, e **só** se o KYC estiver aprovado |

Corolários que valem regra:

- **`Confirmado` libera a entrega, não o repasse.** É `Confirmado` do sinal
  (mais contrato assinado) que move o pedido para `Confirmado` e reserva a data.
- **`Liquidado` não é `Repassado`.** Dinheiro compensado com KYC pendente fica
  retido no PSP. O fotógrafo precisa ver isso no back-office, não descobrir
  pelo extrato. Ver [RN-FIN-030](06-REGRAS-DE-NEGOCIO.md).
- A tela financeira do estúdio mostra as três colunas separadas: **a receber,
  liquidado, repassado**. Nunca um número só chamado "faturamento".

## 3. Dicionário de estados

Definição de negócio de cada estado. As transições permitidas, com guardas e
eventos, estão em [05 · Máquinas de estado](05-MAQUINAS-DE-ESTADO.md).

### Pedido — `OrderStatus`

| Estado | O que significa na vida real |
|---|---|
| `Rascunho` | Cliente começou a preencher e não enviou. Ninguém no estúdio foi notificado |
| `Enviado` | Cliente enviou. Caiu na caixa do fotógrafo |
| `EmAnalise` | Fotógrafo abriu, está checando data e viabilidade |
| `OrcamentoEnviado` | Proposta enviada, com validade correndo |
| `Aprovado` | Cliente aceitou o valor. Ainda não pagou nem assinou |
| `Confirmado` | **Marco.** Sinal `Confirmado` + contrato `Assinado`. A data está reservada |
| `Agendado` | Detalhes logísticos fechados na agenda (horário, deslocamento, equipe) |
| `Realizado` | O evento aconteceu |
| `EmEdicao` | Fotógrafo está tratando as imagens |
| `Entregue` | Galeria disponível e saldo quitado |
| `Concluido` | Ciclo encerrado. Seleção fechada, download feito ou prazo vencido |
| `EmEspera` | Parado por decisão de alguém (cliente pensando, estúdio aguardando informação) |
| `Recusado` | Fotógrafo não vai atender (data cheia, fora de escopo) |
| `Expirado` | Orçamento venceu sem resposta. Vem de job, não de gente |
| `CanceladoPeloCliente` | Desistência do cliente. Política de reembolso se aplica |
| `CanceladoPeloEstudio` | Cancelamento pelo estúdio. Reembolso integral por padrão |
| `Reagendado` | Data mudou. Mantém pedido e pagamentos, gera novo `Booking` |

> **`Agendado` na prática.** Quem reserva a data é `Confirmado` — está escrito
> assim de propósito. `Agendado` só ganha função quando o módulo de
> [Agenda](04-MODULOS-E-AGREGADOS.md) existir (E5). Entre a E3 e a E5 o
> pedido pode ir direto de `Confirmado` para `Realizado`. Ver a decisão
> registrada em [05 · Máquinas de estado](05-MAQUINAS-DE-ESTADO.md).

### Pagamento e parcela

| Estado | Significado |
|---|---|
| `Pendente` | Cobrança criada no PSP, ninguém pagou |
| `Processando` | Pagamento em curso (Pix aguardando, cartão em análise) |
| `Autorizado` | Cartão autorizado, ainda não capturado. **Não existe em Pix nem boleto** |
| `Confirmado` | PSP reconheceu o pagamento |
| `Liquidado` | Compensado no PSP |
| `Repassado` | Split creditado na subconta |
| `Recusado` | Cartão negado, Pix não pago no prazo do QR |
| `Expirado` | Prazo da cobrança venceu |
| `EstornoSolicitado` | Estorno pedido, aguardando o PSP |
| `Estornado` | Estorno total efetivado |
| `EstornoParcial` | Parte devolvida |
| `Chargeback` | Contestação junto à bandeira/emissor |
| `EmDisputa` | Contestação em análise, com prazo de defesa |

### Repasse — `PayoutStatus`

| Estado | Significado |
|---|---|
| `Agendado` | Repasse programado pelo PSP |
| `EmTransito` | Enviado, aguardando confirmação |
| `Liquidado` | Caiu na conta do fotógrafo |
| `Falhou` | Erro técnico ou dado bancário inválido |
| `BloqueadoKyc` | KYC não aprovado. Dinheiro retido no PSP |
| `Retido` | Retenção por disputa, chargeback ou ordem judicial |

### Contrato — `ContractStatus`

`Rascunho` → `Enviado` → `Visualizado` → `Assinado`
Saídas: `Recusado`, `Expirado`, `Cancelado`

### Galeria — `GalleryStatus`

| Estado | Significado |
|---|---|
| `EmPreparo` | Upload e geração de derivadas em curso. Cliente não vê |
| `Disponivel` | Cliente pode abrir |
| `EmSelecao` | Cliente escolhendo favoritas, dentro do limite do pacote |
| `SelecaoFechada` | Escolha travada. Libera edição final e original |
| `Entregue` | Alta resolução liberada, saldo quitado |
| `BloqueadaPorPendencia` | Saldo em aberto: abre em baixa resolução com marca d'água, download travado |
| `Expirada` | Passou do prazo contratual. Vai para storage frio |
| `Arquivada` | Fora do acesso normal, recuperável sob demanda |

### KYC do fotógrafo — `KycStatus`

`NaoIniciado` → `EmAnalise` → `Aprovado`
Saídas: `PendenteDocumento`, `Reprovado`, `Suspenso`

## 4. Termos proibidos

Palavras que criam ambiguidade. Não usar em código, tela nem documento.

| Não use | Use | Por quê |
|---|---|---|
| "usuário" para o cliente final | `Client` / cliente | `User` é conta de acesso; `Client` é quem contrata. São tabelas diferentes |
| "entrada", "reserva", "adiantamento" | **sinal** / `Deposit` | Três nomes para a mesma coisa geram três telas diferentes |
| "pago" sozinho | `Confirmado`, `Liquidado` ou `Repassado` | Ver seção 2. "Pago" é a origem do ticket de suporte |
| "álbum" | **galeria** (`Gallery`) ou **coleção** (`Collection`) | Galeria é privada e de entrega; coleção é pública e curada |
| "foto" para o registro de banco | `Photo` (entidade) vs `PhotoVariant` (arquivo) | Uma foto tem N arquivos. Confundir os dois quebra o cálculo de storage |
| "taxa" | **comissão** (`PlatformFee`) ou **taxa do PSP** | São dois descontos diferentes na mesma transação |
| "aprovado" para pagamento | `Confirmado` (pagamento) / `Aprovado` (pedido) | `Aprovado` é o cliente aceitando o orçamento, não o dinheiro entrando |
| "cancelar" sem sujeito | `CanceladoPeloCliente` / `CanceladoPeloEstudio` | A política de reembolso depende de quem cancelou |

## 5. Siglas

| Sigla | Significado |
|---|---|
| PSP | Payment Service Provider — Asaas, Pagar.me, Mercado Pago |
| KYC | Know Your Customer — verificação de identidade do recebedor, feita pelo PSP |
| RLS | Row Level Security — política de acesso por linha no PostgreSQL |
| ISR | Incremental Static Regeneration — cache de página do Next.js |
| LQIP | Low Quality Image Placeholder — miniatura minúscula embutida no HTML |
| RN | Regra de Negócio — ver [06](06-REGRAS-DE-NEGOCIO.md) |
| ADR | Architecture Decision Record — ver [adr/](adr/README.md) |
| SAQ-A | Nível mais leve de conformidade PCI-DSS, para quem nunca toca dado de cartão |
| WABA | WhatsApp Business Account |
