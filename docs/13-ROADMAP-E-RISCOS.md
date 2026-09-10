# 13 · Roadmap e riscos

Cinco etapas. Cada uma termina em algo **demonstrável e vendável** — não em
"camada de infraestrutura concluída". A alternativa é seis meses sem nada na
mão, e é assim que projeto de um dev só morre.

---

## 1. Ordem

| Etapa | Entrega | Marco | Detalhe |
|---|---|---|---|
| **E1 · fundação** | tenant, auth, RLS, resolução por subdomínio, catálogo de serviços, portfólio público com ISR e tema `editorial` | **Um fotógrafo real já usa como site oficial dele** | [E1](etapas/E1-FUNDACAO.md) |
| **E2 · comercial** | cadastro do cliente, pedido, briefing dinâmico, ficha de direção em PDF, back-office de pedidos | **Substitui o WhatsApp na entrada de lead** | [E2](etapas/E2-COMERCIAL.md) |
| **E3 · dinheiro** | PSP, onboarding + KYC, conta de repasse, split, sinal e saldo, webhooks, conciliação, painel financeiro | **A plataforma passa a faturar** | [E3](etapas/E3-DINHEIRO.md) |
| **E4 · entrega** | upload, derivadas, galeria privada, seleção, ZIP, links de compartilhamento, expiração | **Fecha o ciclo — e liga o canal de indicação** | [E4](etapas/E4-ENTREGA.md) |
| **E5 · agenda e contrato** | disponibilidade, bloqueio de datas, contrato com assinatura, notificações e lembretes, tema `imersivo` | **Produto completo** | [E5](etapas/E5-AGENDA-E-CONTRATO.md) |

### Por que E3 antes de E4

A tentação é fazer a galeria antes: é a parte visível, a que dá orgulho de
mostrar. É a escolha errada.

**Galeria bonita sem cobrança é um custo. Cobrança sem galeria já é um
negócio.** Terminada a E3, o produto fatura com a entrega ainda no Google
Drive. Terminada a E4 sem a E3, o produto serve gigabytes de graça.

### Estimativa

De **5 a 7 meses** para as cinco etapas, para um desenvolvedor meio período.
Isso não é um cronograma: é a ordem de grandeza que impede a decisão errada de
"faço tudo numa v1 de dois meses".

| Etapa | Ordem de grandeza |
|---|---|
| E1 | 4 a 6 semanas |
| E2 | 4 a 6 semanas |
| E3 | 5 a 7 semanas — **mais** a espera de habilitação no PSP, que corre em paralelo desde a E1 |
| E4 | 4 a 6 semanas |
| E5 | 3 a 5 semanas |

## 2. Critério de aceite por etapa

Uma etapa não termina porque o código compila. Termina quando isto é verdade:

### E1
- [ ] Um tenant real publicado em `{slug}.prata.app`, com portfólio dele
- [ ] `TenantIsolationTests` passando contra Postgres real
- [ ] A API se recusa a subir se conectar como dono das tabelas ([RN-TEN-012](06-REGRAS-DE-NEGOCIO.md))
- [ ] Lighthouse CI verde na rota pública: LCP ≤ 2,0 s, CLS ≤ 0,05, SEO 100
- [ ] Conta no PSP aberta e **habilitação de split solicitada**
- [ ] O fotógrafo piloto trocou o link da bio do Instagram para o novo site

O último item é o que prova a etapa. Os outros são pré-requisito dele.

### E2
- [ ] Pedido entra pelo portal, com briefing condicional por tipo de serviço
- [ ] Ficha de direção em PDF, uma página, gerada e usada num evento real
- [ ] Nenhuma resposta sensível aparece em log — teste de log passando
- [ ] O fotógrafo piloto parou de coletar briefing por WhatsApp

### E3
- [ ] Sinal cobrado com split, liquidado e **repassado** em produção
- [ ] Webhook idempotente comprovado: mesmo evento 3× → uma transição
- [ ] Conciliação diária rodando, com divergência semeada sendo detectada
- [ ] KYC pendente visível no back-office do fotógrafo
- [ ] **Primeira comissão recebida na conta da plataforma**

### E4
- [ ] Galeria de casamento real entregue: 800 fotos, upload direto, derivadas
- [ ] `BloqueadaPorPendencia` funcionando: saldo pago destrava a alta resolução
- [ ] ZIP de 20 GB montado em background sem timeout
- [ ] Link de convidado acessado por mais de 15 pessoas distintas
- [ ] Custo de storage do mês conferido contra a estimativa de [09](09-GALERIAS-E-ENTREGA.md)

### E5
- [ ] Data reservada automaticamente na confirmação, sem dupla reserva
- [ ] Contrato assinado com hash, IP e timestamp, em pedido real
- [ ] Lembretes automáticos de saldo e de assinatura funcionando
- [ ] 5 tenants ativos transacionando

## 3. Riscos

| Risco | Impacto | Mitigação | Onde |
|---|---|---|---|
| **Vazamento entre tenants** | **crítico** | query filter + RLS + `TenantId` explícito + teste que tenta ler o vizinho e precisa falhar + gate no CI | [03](03-MULTI-TENANCY.md) |
| Custo de storage e egress | alto | Cloudflare R2 (egress zero), expiração contratual de galeria, storage frio após N meses | [09](09-GALERIAS-E-ENTREGA.md), [ADR-0008](adr/ADR-0008-storage-r2-derivadas-no-worker.md) |
| KYC travando repasse | alto | onboarding no PSP **antes** de publicar o portfólio; situação visível no back-office | [08](08-PAGAMENTOS-SPLIT.md) |
| **Habilitação de split negada ou demorada pelo PSP** | alto | solicitar na E1; ter Pagar.me como plano B atrás do mesmo port | [E1](etapas/E1-FUNDACAO.md) |
| Dado sensível no briefing | alto | acomodação em vez de diagnóstico, consentimento específico, retenção definida, ausência em log | [07](07-BRIEFING.md), [12](12-SEGURANCA-E-LGPD.md) |
| Imagem de menores | alto | consentimento do responsável no contrato + flag na galeria bloqueando portfólio | [12](12-SEGURANCA-E-LGPD.md) |
| Chargeback após a entrega | médio | contrato assinado + log de aceite + entrega registrada; Pix no sinal | [08](08-PAGAMENTOS-SPLIT.md) |
| SEO multi-tenant | médio | subdomínio indexável desde a E1, `sitemap.xml` e dados estruturados por tenant; domínio próprio na E5 | [ADR-0004](adr/ADR-0004-subdominio-na-v1.md) |
| **Orçamento de performance estourado pela camada WebGL** | médio | DOM primeiro, canvas sobreposto; chunk isolado após o LCP; Lighthouse CI quebrando o build | [16](16-FRONTEND-E-EXPERIENCIA.md), [ADR-0010](adr/ADR-0010-motion-e-webgl.md) |
| Concorrência madura | médio | o diferencial é **briefing de direção + split real**, não a galeria. Não tentar ganhar da Pixieset em galeria | [00](00-VISAO-E-ESCOPO.md) |
| **Dev único, meio período** | médio | escopo negativo agressivo, serviço gerenciado, teste de domínio em tudo que toca dinheiro | [00](00-VISAO-E-ESCOPO.md) |
| Licença de dependência mudando | baixo | MediatR e FluentAssertions já ficaram pagos e foram substituídos. ImageSharp e QuestPDF têm limite de receita — reconferir antes de faturar | [`Directory.Packages.props`](../Directory.Packages.props) |
| Abandono de briefing longo | baixo | condicional por serviço, autosave, retomada por link, progresso por bloco | [07](07-BRIEFING.md) |

## 4. Decisões fechadas

Registradas como ADR. Cada uma tem contexto, alternativas e consequência.

| # | Decisão |
|---|---|
| [ADR-0001](adr/ADR-0001-dotnet-10-clean-architecture.md) | .NET 10 LTS, Clean Architecture, dispatcher próprio sem MediatR |
| [ADR-0002](adr/ADR-0002-banco-unico-tenantid-rls.md) | banco único com `tenant_id` + RLS obrigatória |
| [ADR-0003](adr/ADR-0003-modelo-de-receita-comissao.md) | receita por **comissão por transação** na v1, sem assinatura |
| [ADR-0004](adr/ADR-0004-subdominio-na-v1.md) | **só subdomínio** na v1; domínio próprio na E5 |
| [ADR-0005](adr/ADR-0005-psp-asaas-com-port.md) | **Asaas** atrás do port `IPaymentGateway` |
| [ADR-0006](adr/ADR-0006-assinatura-eletronica-propria.md) | **aceite próprio** com trilha de auditoria; port trocável |
| [ADR-0007](adr/ADR-0007-whatsapp-wame-na-v1.md) | **wa.me** na v1; Cloud API na E5 |
| [ADR-0008](adr/ADR-0008-storage-r2-derivadas-no-worker.md) | **Cloudflare R2** + derivadas no worker |
| [ADR-0009](adr/ADR-0009-idioma-do-codigo.md) | identificadores em inglês, **enums de estado em português** |
| [ADR-0010](adr/ADR-0010-motion-e-webgl.md) | GSAP + Lenis + R3F; **PixiJS recusado**; orçamento como gate de CI |

## 5. Decisões ainda abertas

Nenhuma bloqueia a E1. Todas devem ser fechadas antes da etapa indicada.

| Decisão | Quando decidir | Nota |
|---|---|---|
| Subir um `Booking` mínimo já na E3, só com bloqueio de data? | **E3** | ~2 dias de esforço; elimina o risco de dupla reserva no período em que o produto já cobra e ainda não tem agenda. **Recomendação: fazer.** Ver [05](05-MAQUINAS-DE-ESTADO.md) |
| Emissão de nota fiscal da comissão da plataforma | antes de faturar (E3) | é questão contábil, não de código. Precisa de contador |
| Enquadramento controlador/operador no termo de uso | antes de publicar (E1) | precisa de advogado. Ver [12](12-SEGURANCA-E-LGPD.md), seção 7 |
| Preço do excedente de seleção: por foto ou em bloco | **E4** | afeta a UI do upsell, não o modelo |
| Antecipação de recebível de cartão | pós-E3 | o PSP oferece; a taxa decide |
| Assinatura mensal como segundo modelo de receita | v2 | só depois de prova de valor. Ver [ADR-0003](adr/ADR-0003-modelo-de-receita-comissao.md) |
| Storage frio: R2 Infrequent Access ou B2? | **E4** | decidir com o custo real medido, não com tabela de preço |
| App móvel para seleção de fotos | v2 | só se a seleção no navegador provar atrito real |

## 6. O que validar com fotógrafo real antes de codar

Este escopo foi escrito de fora. Dois ou três fotógrafos reais mudam duas
coisas, e é bom saber quais **antes** de escrever o código delas:

| O que perguntar | O que pode mudar |
|---|---|
| Como você fecha um casamento hoje, passo a passo? | os estados do pedido ([05](05-MAQUINAS-DE-ESTADO.md)) — a lista atual tem 17, e provavelmente 4 não existem na prática |
| Quanto de sinal você cobra, e quando o saldo vence? | faixa de 30–50% e o prazo do saldo |
| Você aceitaria pagar comissão por transação? Quanto? | o modelo de receita ([ADR-0003](adr/ADR-0003-modelo-de-receita-comissao.md)) |
| Você já perdeu ensaio porque a pessoa não gostou de si mesma? | o valor percebido do briefing — a premissa do produto |
| Você postaria foto de cliente sem perguntar? | o peso do `PortfolioConsent` |
| Quantos meses você mantém a galeria no ar? | o prazo de expiração e o custo de storage |
| O que você faz quando o cliente não paga o saldo? | se `BloqueadaPorPendencia` é aceitável ou constrangedor demais para ele usar |

As duas primeiras linhas são as que mais provavelmente invalidam parte deste
documento. Isso é esperado: **o escopo foi feito para ser cortado e discutido,
não seguido à risca.**
