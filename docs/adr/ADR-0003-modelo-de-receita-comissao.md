# ADR-0003 · Receita por comissão por transação na v1

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E3

## Contexto

Três modelos possíveis: comissão por transação, assinatura mensal por tenant,
ou híbrido. A escolha muda o que se constrói: assinatura exige um módulo de
billing **da própria plataforma** — recorrência, régua de inadimplência,
suspensão de tenant — que é um segundo fluxo de dinheiro, com sua própria
conciliação.

O contexto de aquisição pesa: um fotógrafo que ainda não faturou nada pelo
Prata não tem por que pagar assinatura, e cobrar antes de provar valor mata o
primeiro cliente — que é justamente o mais difícil de conseguir.

## Decisão

**Comissão por transação, via split do PSP.** Percentual configurável por
tenant, padrão 8%, aplicado sobre cada cobrança. Sem mensalidade na v1.

- `SplitRule` versionada, com `VigenteDe`/`VigenteAte`.
- A regra aplicada é **snapshot na criação da cobrança**: mudar a comissão
  nunca reescreve cobrança antiga
  ([RN-FIN-032](../06-REGRAS-DE-NEGOCIO.md)).
- Nenhum módulo de assinatura de tenant na v1.

## Alternativas consideradas

| Alternativa | Por que não agora |
|---|---|
| Assinatura mensal por tenant | MRR previsível, mas exige billing próprio da plataforma e cobra antes de provar valor. É o modelo certo **depois** da prova |
| Híbrido (plano + comissão reduzida) | modelo da Pixieset/HoneyBook e provavelmente o destino final. Dobra o escopo financeiro da v1: split **e** recorrência, cada um com conciliação própria |
| Comissão fixa em reais por pedido | simples, mas injusto nas duas pontas: caro no ensaio de R$ 400, irrelevante no casamento de R$ 12.000 |
| Taxa sobre storage | alinha custo e receita, mas é incompreensível para o cliente e pune quem entrega mais foto |

## Consequências

### Boas

- Barreira de entrada zero: o fotógrafo só paga quando fatura.
- Um fluxo de dinheiro a construir, não dois.
- Receita cresce com o sucesso do tenant — o incentivo está alinhado.
- `SplitRule` versionada já permite negociar taxa por tenant desde o dia um.

### Ruins e o que fazemos a respeito

- **Receita zero até a E3.** E1 e E2 são investimento puro. Consequência
  direta: **a E3 não pode escorregar**, e a habilitação de split no PSP é
  tarefa da E1 ([13 · Roadmap](../13-ROADMAP-E-RISCOS.md)).
- **Sem MRR previsível.** Receita sazonal, com o calendário de casamento.
  Aceito na v1.
- **Tenant que usa só o portfólio não gera receita** — e gera custo de
  storage e de CDN. Mitigado pela expiração de galeria e pela política de
  storage frio. Se virar padrão, é sinal de que a assinatura precisa entrar.
- **Incentivo torto:** o fotógrafo pode combinar Pix por fora para evitar a
  comissão. É risco real. A mitigação não é técnica, é de valor entregue —
  contrato, galeria e cobrança automática precisam valer mais que a comissão.
  Vale medir a proporção de pedidos confirmados sem pagamento na plataforma.

## Revisão

Reavaliar quando houver **10 tenants transacionando** ou 6 meses após a E3, o
que vier primeiro. A decisão esperada nessa revisão é o híbrido.
