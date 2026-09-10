# E2 · Comercial

**Marco:** substitui o WhatsApp na entrada de lead.
**Ordem de grandeza:** 4 a 6 semanas, meio período.
**Depende de:** [E1](E1-FUNDACAO.md).

A etapa termina quando o fotógrafo piloto **para de coletar briefing por
WhatsApp** — não quando o formulário compila.

---

## 1. Escopo

| Entra | Não entra |
|---|---|
| `Client` e cadastro simples | pagamento (E3) |
| `Order`, `OrderItem`, `Quote`, `Discount` | contrato e assinatura (E5) |
| Máquina de estados do pedido, de `Rascunho` a `Aprovado` | galeria (E4) |
| Briefing: template, perguntas, condicional, respostas | agenda de verdade (E5) |
| Consentimento do bloco sensível | Cloud API do WhatsApp (E5) |
| Ficha de direção em PDF | |
| Back-office de pedidos | |
| E-mail transacional + link `wa.me` | |
| Job de expiração de orçamento | |

Os estados de `Confirmado` para frente ficam para a E3 e a E5: sem pagamento e
sem contrato, `Confirmado` não é alcançável ([RN-COM-020](../06-REGRAS-DE-NEGOCIO.md)).

## 2. Tarefas

### 2.1 Cliente e pedido

- [ ] `Client` com `UNIQUE (tenant_id, email)`; pedido novo do mesmo e-mail
      reusa o cliente ([RN-COM-012](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Cadastro simples: nome, e-mail, WhatsApp, canal preferido
- [ ] `Order` com snapshot de nome e preço em `OrderItem`
      ([RN-CAT-004](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Total = Σ itens − desconto, recalculado só até `OrcamentoEnviado`
      ([RN-COM-001](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Data pretendida no passado é rejeitada
      ([RN-COM-010](../06-REGRAS-DE-NEGOCIO.md))
- [ ] `Quote` com versão e `ValidoAte` **gravado**, não calculado na leitura
      ([RN-COM-013](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Máquina de estados: `Rascunho` → `Enviado` → `EmAnalise` →
      `OrcamentoEnviado` → `Aprovado`, mais `Recusado`, `Expirado`,
      `EmEspera`, `CanceladoPeloCliente`
- [ ] **Teste parametrizado** de todos os pares estado × transição
      ([15](../15-ESTRATEGIA-DE-TESTES.md), seção 2)
- [ ] Job `ExpirarOrcamentos` diário
- [ ] Checagem de conflito de data contra pedidos confirmados do tenant
      ([RN-AGD-030](../06-REGRAS-DE-NEGOCIO.md)) — substituto da agenda até a E5

### 2.2 Briefing — o diferencial

- [ ] `BriefingTemplate`, `Question`, `Option`, versionado e publicável
- [ ] Os dez `QuestionType` de [07](../07-BRIEFING.md), seção 3
- [ ] `Answer` com **snapshot do texto e do tipo da pergunta**
      ([RN-BRF-002](../06-REGRAS-DE-NEGOCIO.md))
- [ ] `value` em `jsonb`, validado pelo tipo
      ([RN-BRF-004](../06-REGRAS-DE-NEGOCIO.md))
- [ ] `VisibleWhen` avaliado no cliente **e revalidado no servidor**; circular
      rejeitado ([RN-BRF-003](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Obrigatória oculta por condicional **não** bloqueia o envio
      ([RN-BRF-010](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Autosave parcial sem validar obrigatoriedade
      ([RN-BRF-011](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Retomada por link, com progresso por bloco
- [ ] Upload de referência (B8, D2) por URL assinada
- [ ] **Seed dos dez templates** com a composição de [07](../07-BRIEFING.md),
      seção 4, **copiados** para cada tenant novo
      ([RN-BRF-001](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Editor de perguntas no back-office
- [ ] Teste "B2 sugere o teste das duas selfies quando a resposta é *não sei*"

### 2.3 Bloco sensível e LGPD

Esta subseção é a que não pode ser adiada "para depois", porque o dado entra
na base desde o primeiro briefing.

- [ ] `briefing_consent` com escopo, timestamp, IP, user-agent e **snapshot do
      texto da finalidade exibido**
- [ ] Bloco B inteiro opcional; sem consentimento, resposta sensível é
      rejeitada ([RN-BRF-020](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Revisão do template rejeita pergunta sensível em formato de diagnóstico
      ([RN-BRF-021](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Autorização: sensível só para `tenant.owner` e staff designado
      ([RN-BRF-030](../06-REGRAS-DE-NEGOCIO.md))
- [ ] **Teste de log** com marcador único, provando ausência em log, trace e
      e-mail ([RN-BRF-031](../06-REGRAS-DE-NEGOCIO.md),
      [RN-LGP-005](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Job `ExpurgarBriefingSensivel`, 12 meses após a entrega, com auditoria
      ([RN-LGP-004](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Revogação de consentimento apaga as respostas e mantém o pedido
      ([RN-LGP-003](../06-REGRAS-DE-NEGOCIO.md))
- [ ] `AuditLog` na **leitura** de resposta sensível
      ([RN-AUD-003](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Inventário de dado pessoal atualizado em
      [12](../12-SEGURANCA-E-LGPD.md), seção 4

### 2.4 Ficha de direção

- [ ] QuestPDF renderizando o layout de [07](../07-BRIEFING.md), seção 6
- [ ] **Uma página, sempre** — corte por prioridade, não truncamento
      ([RN-BRF-040](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Escalas como bolinhas (`●●○○○`), não números
- [ ] Alerta de aquecimento **derivado** de B4 < 3
- [ ] Bloco ATENÇÃO juntando C2, C3, C4
- [ ] Imagem de referência de B8 embutida
- [ ] Nome do arquivo sem dado pessoal: `ficha-{orderId}.pdf`
- [ ] Servida por URL assinada de TTL curto, **nunca anexada em e-mail**

### 2.5 Back-office de pedidos

- [ ] Lista com filtro por status, consulta em Dapper com `TenantId` explícito
- [ ] Detalhe do pedido com briefing, respeitando a autorização do sensível
- [ ] Montagem de orçamento: itens, adicionais, desconto, validade
- [ ] Ações: analisar, orçar, recusar, pôr em espera
- [ ] **Lista de pendências acionáveis**, com link `wa.me` pronto ao lado de
      cada uma
- [ ] Botão de ficha de direção

### 2.6 Notificações

- [ ] `INotifier` com canal de e-mail (SMTP)
- [ ] Idempotência por `(tenant, destinatário, tipo, chave)`
      ([RN-NOT-001](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Falha de envio **não** desfaz a transação de negócio
      ([RN-NOT-003](../06-REGRAS-DE-NEGOCIO.md))
- [ ] `From` amigável do tenant, envelope do domínio da plataforma
      ([RN-NOT-011](../06-REGRAS-DE-NEGOCIO.md))
- [ ] Gerador de link `wa.me` pré-preenchido, disparado por ação humana
      ([RN-NOT-010](../06-REGRAS-DE-NEGOCIO.md))
- [ ] E-mails da etapa: pedido recebido, orçamento enviado, orçamento
      expirando, orçamento aprovado

## 3. Critério de aceite

- [ ] Pedido entra pelo portal, com briefing condicional por tipo de serviço
- [ ] Briefing de casamento respondido do início ao fim em celular real
- [ ] Ficha de direção gerada e **usada num evento real** pelo piloto
- [ ] Teste de log passando: nenhuma resposta sensível em log ou e-mail
- [ ] Job de expiração de orçamento rodando
- [ ] Job de expurgo sensível implementado e agendado — mesmo sem nada para
      expurgar ainda
- [ ] **O fotógrafo piloto parou de coletar briefing por WhatsApp**

## 4. Riscos da etapa

| Risco | Mitigação |
|---|---|
| Briefing longo derruba a conversão | condicional por serviço, autosave, retomada, progresso por bloco. Medir taxa de abandono por bloco desde o primeiro dia |
| Dado sensível entrando em log sem ninguém notar | teste de log com marcador único, obrigatório |
| Ficha de direção passando de uma página | corte por prioridade implementado desde o início, não depois |
| Template editado quebrando briefing antigo | snapshot de texto e tipo em `Answer`, testado |
| Fotógrafo achar as perguntas do bloco B invasivas | é a hipótese central do produto. **Perguntar aos dois pilotos antes de implementar o bloco inteiro** |

O último risco é o mais importante da etapa: se o fotógrafo achar o bloco B
invasivo, o diferencial do produto precisa ser repensado — e é muito mais
barato descobrir isso numa conversa que depois de implementar 40 perguntas.
