# E5 · Agenda e contrato

**Marco:** produto completo.
**Ordem de grandeza:** 3 a 5 semanas, meio período.
**Depende de:** E1 a E4.

> **Esqueleto de propósito.** Ver a nota em [E3](E3-DINHEIRO.md).

---

## 1. Objetivo

Fechar as duas lacunas que sobraram: a data reservada de verdade e o contrato
assinado com prova. Com isso, `Confirmado` passa a significar exatamente o que
[05](../05-MAQUINAS-DE-ESTADO.md) diz que significa.

## 2. Escopo

| Entra | Não entra |
|---|---|
| `Booking`, `Availability`, `BlackoutDate` | assinatura ICP-Brasil |
| Buffer de deslocamento e `EXCLUDE` no banco | app móvel |
| Estado `Agendado` ganhando função | nota fiscal |
| `Contract`, `Signature`, aceite próprio | assinatura mensal de tenant |
| Contrato imutável após assinado | |
| Régua de lembretes (saldo, assinatura, evento) | |
| **Domínio próprio por tenant** (CNAME + SSL) | |
| **WhatsApp Cloud API**, opcional por tenant | |
| Reagendamento | |

## 3. Decisões a fechar no início da etapa

| Decisão | Nota |
|---|---|
| Manter o aceite próprio ou passar para ZapSign | depende de a assinatura própria ter sido questionada em algum caso real até aqui. Ver [ADR-0006](../adr/ADR-0006-assinatura-eletronica-propria.md) |
| Cloud API do WhatsApp: quem paga a conversa | o tenant, provavelmente. Muda o modelo de receita marginalmente |
| Migração de subdomínio para domínio próprio: política de `301` | toda URL indexada precisa redirecionar, e o subdomínio precisa continuar respondendo por tempo indeterminado |

## 4. Critério de aceite

- [ ] Data reservada automaticamente em `PedidoConfirmado`, sem dupla reserva
- [ ] Reserva concorrente barrada **pelo banco** (`EXCLUDE`), não pela
      aplicação
- [ ] Buffer de deslocamento respeitado
- [ ] Contrato assinado com hash do PDF, IP, user-agent e timestamp, em pedido
      real
- [ ] Contrato assinado é imutável; correção gera novo com referência
- [ ] Contrato contendo as três cláusulas obrigatórias: expiração de galeria,
      consentimento de uso de imagem, consentimento do responsável por menor
- [ ] Lembretes automáticos de saldo e de assinatura funcionando
- [ ] Um tenant rodando em **domínio próprio** com SSL automático e `301` do
      subdomínio
- [ ] **5 tenants ativos transacionando**

## 5. Riscos da etapa

| Risco | Mitigação |
|---|---|
| Dupla reserva por condição de corrida | `EXCLUDE USING gist` no Postgres, não checagem na aplicação |
| Assinatura própria questionada em disputa real | hash + IP + user-agent + timestamp + `AuditLog`; port pronto para trocar por ZapSign |
| PDF regerado com fonte diferente invalidando o hash | o PDF assinado é **armazenado**, nunca regerado sob demanda |
| Domínio próprio gerando ticket de DNS | onboarding guiado, com verificação de propriedade e diagnóstico na tela |
| SSL não renovando | alarme com 14 dias de antecedência |
| Cloud API exigindo WABA por tenant | opt-in; `wa.me` continua sendo o padrão |
| Régua de lembrete virando spam | idempotência por `(tenant, destinatário, tipo, chave)` e limite de 3 lembretes por pendência |

## 6. Depois da E5

O produto está completo no escopo v0.1. As candidatas naturais a v2, na ordem
em que provavelmente importam:

| Candidata | Gatilho para considerar |
|---|---|
| **Assinatura mensal** como segundo modelo de receita | 10 tenants transacionando ou 6 meses após a E3. Ver [ADR-0003](../adr/ADR-0003-modelo-de-receita-comissao.md) |
| App móvel para seleção de fotos | se a seleção no navegador provar atrito real |
| Emissão de nota fiscal | quando o volume justificar a integração |
| Antecipação de recebível de cartão | quando a taxa fizer sentido para o fotógrafo |
| Tema novo | quando os três atuais não cobrirem um nicho pedido por mais de um tenant |
| `imgproxy` no lugar do ImageSharp | se o limite de licença for atingido ou o volume de derivadas pesar |

E a decisão que se toma **antes** de qualquer uma delas: reler
[13 · Roadmap e riscos](../13-ROADMAP-E-RISCOS.md), seção 6, e conversar com
os fotógrafos que já estão usando. A esta altura eles são a melhor fonte de
roadmap que existe.
