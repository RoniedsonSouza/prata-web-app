# ADR-0007 · `wa.me` na v1; Cloud API na E5

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E2 (`wa.me`) · E5 (Cloud API)

## Contexto

No Brasil, WhatsApp é o canal real de comunicação com o cliente final. A taxa
de leitura de e-mail transacional é baixa, e lembrete de evento e aviso de
galeria expirando perdem eficácia se ficarem só no e-mail.

Mas há um problema específico de multi-tenant que costuma ser descoberto tarde:
**a Cloud API oficial exige uma WhatsApp Business Account por remetente.**
Duas saídas, ambas ruins na v1:

1. Cada fotógrafo cria a própria WABA — onboarding pesado, com verificação de
   negócio na Meta, para alguém que quer só publicar um portfólio.
2. A plataforma envia do número dela em nome do fotógrafo — o cliente final
   recebe mensagem de um número desconhecido falando do casamento dele. Confuso
   e ruim para a marca do fotógrafo.

## Decisão

**v1 (E2):**
- **Link `wa.me` pré-preenchido**, gerado no back-office e disparado por **ação
  humana** do fotógrafo. Nenhum envio automático por WhatsApp
  ([RN-NOT-010](../06-REGRAS-DE-NEGOCIO.md)).
- **E-mail transacional** cobre todos os avisos automáticos: orçamento,
  confirmação, cobrança, galeria pronta, expiração.
- `INotifier` já com o canal WhatsApp previsto na interface.

**E5:** Cloud API como recurso **opcional por tenant**, para quem já tem WABA
ou quer criar uma.

## Alternativas consideradas

| Alternativa | Por que não agora |
|---|---|
| Cloud API oficial desde a v1 | notificação automática confiável, e o desenvolvedor já tem experiência com ela. Mas exige onboarding de WABA por tenant, template aprovado pela Meta e custo por conversa desde o primeiro cliente |
| Enviar tudo do número da plataforma | o cliente final recebe mensagem de número desconhecido sobre o casamento dele. Ruim para a marca do fotógrafo, que é o dono do relacionamento |
| Só e-mail, sem WhatsApp no roadmap | mais simples e mais barato, mas no Brasil a taxa de leitura de e-mail do cliente final é baixa. Lembrete de evento perde eficácia |
| API não oficial (biblioteca de terceiro) | viola os termos da Meta e derruba o número. Fora de discussão |

## Consequências

### Boas

- Custo zero e zero aprovação de template na v1.
- A mensagem sai do **número do próprio fotógrafo** — que é o certo: o
  relacionamento é dele.
- Nada a manter quando a Meta muda política de template.
- Escopo da E2 fica menor.

### Ruins e o que fazemos a respeito

- **Nenhum aviso automático por WhatsApp.** Lembrete de saldo e de galeria
  expirando dependem do e-mail, com taxa de leitura menor. Mitigação: e-mail
  bem feito (assunto direto, um botão) e a pendência visível no back-office
  para o fotógrafo cobrar por conta dele.
- **Depende de ação humana.** O fotógrafo precisa clicar. Mitigação: a lista
  de pendências do back-office coloca o link `wa.me` ao lado de cada
  pendência, com o texto pronto.
- **Sem registro de entrega.** `wa.me` não confirma leitura nem entrega, então
  `notification` registra apenas "link gerado". Aceito na v1.
