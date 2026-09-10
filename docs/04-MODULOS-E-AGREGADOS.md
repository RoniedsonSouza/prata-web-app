# 04 · Módulos e agregados

Oito contextos. Cada um vira uma pasta em `Domain` e em `Application`, com
seus próprios casos de uso — **não** um `Services/` genérico onde tudo cabe.

Para cada módulo: propósito, agregados, invariantes, eventos publicados e o
que **não** pertence a ele. A última coluna é a que evita o módulo virar
depósito.

---

## 01 · Identidade — Tenant e acesso

Raiz de tudo. Estúdio, plano, domínio, tema, equipe e permissão.

| | |
|---|---|
| **Agregados** | `Tenant` (raiz), `TenantSettings`, `User`, `Role`, `Invite` |
| **Raiz de agregado** | `Tenant` — `TenantSettings` e `Invite` só existem dentro dele. `User` é raiz própria |
| **Invariantes** | slug único global, formato validado, fora da lista de reservados, imutável após publicar o portfólio · pelo menos um `tenant.owner` ativo sempre · `UNIQUE (tenant_id, email)` em `User` · convite expira em 7 dias |
| **Eventos** | `TenantCriado`, `TenantSuspenso`, `TenantReativado`, `MembroConvidado`, `ConviteAceito`, `MembroRemovido`, `TemaAlterado` |
| **Não pertence** | dado de recebimento (é do Financeiro) · conteúdo do portfólio (é da Vitrine) · `platform_user`, que vive fora do modelo de tenant |

`TenantSettings` guarda tema, par tipográfico, paleta, `EffectsEnabled`,
fuso, moeda, política de validade de orçamento e prazo de expiração de
galeria. É lido pelo front público em build/ISR e pelo domínio nas regras.

## 02 · Catálogo — Serviços e pacotes

O que o estúdio vende.

| | |
|---|---|
| **Agregados** | `ServiceType` (raiz), `Package` (raiz), `Addon`, `PriceTable` |
| **Invariantes** | preço > 0 em BRL · pacote pertence a exatamente um `ServiceType` · pacote publicado precisa de nome, preço e nº de fotos incluídas · desativar pacote **não** altera pedido existente |
| **Eventos** | `PacotePublicado`, `PacoteDespublicado`, `PrecoAlterado` |
| **Não pertence** | valor de um pedido específico (é snapshot no `Order`) · desconto negociado (é do Comercial) |

Tipos de serviço previstos de origem: casamento civil, pré-wedding,
aniversário infantil, studio, corporativo, gestante, newborn, formatura, book
15 anos, ensaio de família. O fotógrafo cria os dele; esses são só o seed.

> **Regra que economiza processo:** preço de pacote é **copiado** para o
> `OrderItem` quando o orçamento é montado. Alterar a tabela de preço nunca
> reescreve pedido antigo. Ver [RN-CAT-004](06-REGRAS-DE-NEGOCIO.md).

## 03 · Vitrine — Portfólio público

O que o ISR do Next consome. É o canal de aquisição do fotógrafo.

| | |
|---|---|
| **Agregados** | `Collection` (raiz), `CollectionItem`, `PageContent`, `SeoMeta` |
| **Invariantes** | coleção publicada precisa de ≥ 1 item e uma capa definida · slug de coleção único por tenant · ordem dos itens é explícita, não implícita por data · **foto com `PortfolioConsent = Nao` nunca entra em coleção pública** |
| **Eventos** | `ColecaoPublicada`, `ColecaoDespublicada`, `CapaAlterada`, `ConteudoPublicado` — todos disparam revalidação de ISR |
| **Não pertence** | foto de galeria de cliente (é da Entrega; entra na vitrine por cópia curada e consentida) · métrica de visita (é observabilidade) |

A ligação com o consentimento é cruzada de propósito: o briefing pergunta se
o cliente autoriza uso das imagens
([bloco D](07-BRIEFING.md)), e a resposta vira flag na galeria. A Vitrine
consulta essa flag antes de aceitar o item. Ver
[RN-VIT-005](06-REGRAS-DE-NEGOCIO.md) e
[RN-ENT-030](06-REGRAS-DE-NEGOCIO.md).

## 04 · Comercial — Pedidos e orçamento

O agregado central. Se um módulo merece cuidado extra em teste, é este.

| | |
|---|---|
| **Agregados** | `Order` (raiz), `OrderItem`, `Quote`, `Discount`; `Client` (raiz própria) |
| **Invariantes** | total = Σ itens − desconto, recalculado **só** entre `Rascunho` e `OrcamentoEnviado` · data pretendida não pode estar no passado · desconto ≤ subtotal · `OrderItem` carrega snapshot de nome e preço · transição de status só pela tabela de [05](05-MAQUINAS-DE-ESTADO.md) · `Confirmado` exige sinal `Confirmado` **e** contrato `Assinado` |
| **Eventos** | `PedidoCriado`, `PedidoEnviado`, `OrcamentoEnviado`, `OrcamentoAprovado`, `PedidoConfirmado`, `PedidoRecusado`, `PedidoExpirado`, `PedidoCancelado`, `PedidoRealizado`, `PedidoEntregue`, `PedidoConcluido`, `PedidoReagendado` |
| **Não pertence** | criar cobrança no PSP (é do Financeiro, reagindo a `OrcamentoAprovado`) · reservar data (é da Agenda, reagindo a `PedidoConfirmado`) · gerar galeria (é da Entrega, reagindo a `PedidoRealizado`) |

A última linha é a mais importante do módulo: **o Comercial publica evento, os
outros reagem.** `Order` não conhece `Payment`, `Booking` nem `Gallery`. É o
que mantém o agregado testável sem subir meio sistema.

## 05 · Direção — Briefing

O diferencial do produto. Detalhe completo em [07 · Briefing](07-BRIEFING.md).

| | |
|---|---|
| **Agregados** | `BriefingTemplate` (raiz), `Question`, `Option`; `Answer` (raiz, por pedido); `ShootSheet` |
| **Invariantes** | template pertence a um `(tenant, ServiceType)` · `Answer` guarda **snapshot do texto da pergunta** — editar o template não altera briefing antigo · pergunta com `IsSensitive` exige consentimento específico registrado · `VisibleWhen` referencia só pergunta anterior no mesmo template · resposta obrigatória não vazia antes de `Enviado` |
| **Eventos** | `BriefingIniciado`, `BriefingSalvoParcialmente`, `BriefingConcluido`, `FichaDeDirecaoGerada`, `RespostasSensiveisExpurgadas` |
| **Não pertence** | dado de contato do cliente (é do Comercial, em `Client`) · consentimento de uso de imagem como decisão jurídica (a resposta nasce aqui e vira cláusula no Contrato) |

## 06 · Agenda — Disponibilidade

| | |
|---|---|
| **Agregados** | `Booking` (raiz), `Availability`, `BlackoutDate` |
| **Invariantes** | não existem dois `Booking` ativos sobrepostos no mesmo tenant, considerando buffer de deslocamento · `Booking` só nasce de pedido `Confirmado` · `BlackoutDate` bloqueia criação de `Booking`, não o contrário |
| **Eventos** | `DataReservada`, `DataLiberada`, `DataBloqueada`, `AgendamentoDetalhado` |
| **Não pertence** | decidir se aceita o pedido (é do Comercial, que **consulta** a Agenda) |

> **Dependência de etapa.** Este módulo é E5, mas `PedidoConfirmado` acontece
> na E3. Entre uma e outra, a reserva de data existe apenas como campo em
> `Order` e a checagem de conflito é manual. A alternativa — subir um `Booking`
> mínimo já na E3, só com bloqueio de data e sem buffer nem disponibilidade —
> está avaliada em [05](05-MAQUINAS-DE-ESTADO.md) e em
> [E3](etapas/E3-DINHEIRO.md).

## 07 · Financeiro — Pagamentos e repasse

Módulo mais sensível. Detalhe completo em
[08 · Pagamentos e split](08-PAGAMENTOS-SPLIT.md).

| | |
|---|---|
| **Agregados** | `Payment` (raiz) com `Installment`; `PayoutAccount` (raiz); `SplitRule` (raiz, versionada); `Payout` (raiz); `PaymentEvent` (log de webhook) |
| **Invariantes** | Σ parcelas = total do pagamento · sinal entre 30% e 50% do total · cada `Installment` tem máquina de estados própria · `SplitRule` aplicada é **snapshot na criação da cobrança**, nunca lida do cadastro atual · `Payout` bloqueado enquanto KYC ≠ `Aprovado` · `PaymentEvent.ExternalEventId` é único · estado só muda por webhook verificado ou consulta ativa à API do PSP |
| **Eventos** | `CobrancaCriada`, `PagamentoConfirmado`, `PagamentoLiquidado`, `RepasseAgendado`, `RepasseLiquidado`, `RepasseBloqueadoPorKyc`, `PagamentoEstornado`, `ChargebackAberto`, `DivergenciaDeConciliacao` |
| **Não pertence** | armazenar dado de cartão — **nunca, em lugar nenhum** · decidir se a galeria abre (a Entrega reage a `PagamentoConfirmado`) · nota fiscal |

## 08 · Entrega — Galerias

Maior consumo de infra e maior gerador de indicação. Detalhe em
[09 · Galerias e entrega](09-GALERIAS-E-ENTREGA.md).

| | |
|---|---|
| **Agregados** | `Gallery` (raiz) com `Photo` e `PhotoVariant`; `Selection`; `ShareLink`; `DownloadJob` |
| **Invariantes** | `Photo` pertence a uma `Gallery` · original nunca é alterado nem sobrescrito · `Selection` respeita o limite do pacote; excedente exige `Payment` de upsell **confirmado** · `ShareLink` tem token assinado, senha e expiração · galeria com saldo em aberto fica `BloqueadaPorPendencia` · original só é liberado com `SelecaoFechada` **e** saldo pago |
| **Eventos** | `GaleriaCriada`, `FotoEnviada`, `DerivadasGeradas`, `GaleriaDisponibilizada`, `SelecaoAberta`, `SelecaoFechada`, `LinkCompartilhado`, `DownloadPronto`, `GaleriaExpirando`, `GaleriaExpirada`, `GaleriaBloqueadaPorPendencia` |
| **Não pertence** | cobrar o saldo (é do Financeiro) · publicar no portfólio (é da Vitrine, respeitando consentimento) |

---

## Módulos transversais

Não são contextos de negócio: são capacidades usadas por todos. Ficam em
`Domain/Shared/` e `Application/`, com ports em `Abstractions/`.

### Contrato

| | |
|---|---|
| **Agregados** | `Contract` (raiz), `Signature` |
| **Invariantes** | contrato `Assinado` é **imutável** — correção gera novo contrato com referência ao anterior · `Signature` registra hash SHA-256 do PDF, IP, user-agent e timestamp · contrato precisa conter cláusula de expiração de galeria, consentimento de uso de imagem e, havendo menor, consentimento do responsável |
| **Eventos** | `ContratoEnviado`, `ContratoVisualizado`, `ContratoAssinado`, `ContratoRecusado`, `ContratoExpirado` |
| **Port** | `ISignatureProvider` — implementação própria na v1; ZapSign/Clicksign entram sem tocar no domínio. Ver [ADR-0006](adr/ADR-0006-assinatura-eletronica-propria.md) |

### Notificações

| | |
|---|---|
| **Agregados** | `Notification` (raiz) |
| **Invariantes** | notificação é idempotente por `(tenant, destinatário, tipo, chave de origem)` — job que roda duas vezes não manda dois e-mails · **nunca** carrega resposta sensível de briefing no corpo · registra tentativa, falha e canal |
| **Canais** | e-mail (SMTP) desde a E2; WhatsApp por link `wa.me` na v1, Cloud API na E5. Ver [ADR-0007](adr/ADR-0007-whatsapp-wame-na-v1.md) |
| **Port** | `INotifier` |

### Auditoria

| | |
|---|---|
| **Agregados** | `AuditLog` (append-only) |
| **Invariantes** | **imutável**: sem `UPDATE`, sem `DELETE` — garantido por policy no banco · obrigatório em toda operação que toca dinheiro ou dado pessoal · registra ator, papel, tenant, recurso, ação, antes/depois e IP · nunca contém conteúdo de resposta sensível, só a referência |
| **Retenção** | 60 meses. Ver [12 · Segurança e LGPD](12-SEGURANCA-E-LGPD.md) |

### Outbox

Tabela `outbox_message`, gravada na mesma transação do agregado e entregue
pelo worker. É o que garante que `PedidoConfirmado` não mande e-mail de um
pedido que o commit descartou. Ver
[02 · Arquitetura](02-ARQUITETURA.md), seção 5.3.

---

## Mapa de dependência entre módulos

Quem reage a quem. Setas são **eventos**, nunca chamada direta de um domínio
em outro.

```
 Comercial ──PedidoAprovado──────────► Financeiro   (cria cobrança do sinal)
 Comercial ──PedidoConfirmado────────► Agenda       (reserva a data)
 Comercial ──PedidoConfirmado────────► Notificações
 Comercial ──PedidoRealizado─────────► Entrega      (cria galeria)
 Financeiro ─PagamentoConfirmado─────► Comercial    (avalia ir para Confirmado)
 Financeiro ─PagamentoConfirmado─────► Entrega      (destrava galeria pendente)
 Financeiro ─PagamentoLiquidado──────► Financeiro   (agenda repasse)
 Briefing ───BriefingConcluido───────► Comercial    (habilita envio do pedido)
 Briefing ───BriefingConcluido───────► Notificações (ficha de direção pronta)
 Entrega ────SelecaoFechada──────────► Comercial    (libera edição final)
 Entrega ────GaleriaExpirando────────► Notificações (aviso em 30/7/1 dia)
 Identidade ─TemaAlterado────────────► Vitrine      (revalida ISR)
```

Nenhum módulo referencia entidade de outro. Quando o Financeiro precisa saber
o total do pedido, o valor chega **no evento ou no comando**, não por
navegação de objeto. É essa disciplina que permite testar o Financeiro sem
construir um `Order` inteiro.
