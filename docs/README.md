# Prata · documentação

SaaS vertical multi-tenant de gestão e entrega fotográfica, com portfólio
público, portal do cliente e split de pagamento.

Escopo v0.1 · setembro de 2026 · codinome **Prata**, trocável.

> Este conjunto existe para ser **cortado e discutido**, não seguido à risca.
> Os dois pontos que mais mudam depois de conversar com dois ou três
> fotógrafos reais são os estados do pedido
> ([05](05-MAQUINAS-DE-ESTADO.md)) e o modelo de receita
> ([ADR-0003](adr/ADR-0003-modelo-de-receita-comissao.md)).

## Por onde começar

| Se você quer… | Leia |
|---|---|
| entender o produto em 10 minutos | [00 · Visão e escopo](00-VISAO-E-ESCOPO.md) |
| começar a codar hoje | [E1 · Fundação](etapas/E1-FUNDACAO.md) |
| saber por que a stack é essa | [ADR](adr/README.md) |
| não vazar foto entre tenants | [03 · Multi-tenancy](03-MULTI-TENANCY.md) |
| mexer em dinheiro | [08 · Pagamentos e split](08-PAGAMENTOS-SPLIT.md) |
| deixar o portfólio nível Awwwards | [16 · Front-end e experiência](16-FRONTEND-E-EXPERIENCIA.md) |

## Documentos de referência

| # | Documento | Assunto |
|---|---|---|
| 00 | [Visão e escopo](00-VISAO-E-ESCOPO.md) | o produto, os atores, o que ele **não** é |
| 01 | [Glossário](01-GLOSSARIO.md) | linguagem ubíqua PT↔EN, dicionário de estados |
| 02 | [Arquitetura](02-ARQUITETURA.md) | Clean Architecture, regra de dependência, ports |
| 03 | [Multi-tenancy](03-MULTI-TENANCY.md) | isolamento, RLS, checklist anti-vazamento |
| 04 | [Módulos e agregados](04-MODULOS-E-AGREGADOS.md) | os 8 contextos e suas invariantes |
| 05 | [Máquinas de estado](05-MAQUINAS-DE-ESTADO.md) | transições, guardas e eventos de domínio |
| 06 | [Regras de negócio](06-REGRAS-DE-NEGOCIO.md) | catálogo `RN-XXX-000` numerado e testável |
| 07 | [Briefing](07-BRIEFING.md) | banco de perguntas, modelagem, LGPD do bloco sensível |
| 08 | [Pagamentos e split](08-PAGAMENTOS-SPLIT.md) | subcontas, repasse, webhook, conciliação |
| 09 | [Galerias e entrega](09-GALERIAS-E-ENTREGA.md) | upload, derivadas, seleção, expiração |
| 10 | [API](10-API.md) | superfície, convenções, erros, idempotência |
| 11 | [Modelo de dados](11-MODELO-DE-DADOS.md) | tabelas, índices, papéis do banco, DDL de RLS |
| 12 | [Segurança e LGPD](12-SEGURANCA-E-LGPD.md) | matriz de permissão, base legal, retenção |
| 13 | [Roadmap e riscos](13-ROADMAP-E-RISCOS.md) | E1–E5, critério de aceite, riscos |
| 14 | [Ambientes e operação](14-AMBIENTES-E-OPERACAO.md) | subir local, deploy, jobs, alarmes |
| 15 | [Estratégia de testes](15-ESTRATEGIA-DE-TESTES.md) | pirâmide, Testcontainers, gate de isolamento |
| 16 | [Front-end e experiência](16-FRONTEND-E-EXPERIENCIA.md) | motion, WebGL, temas, orçamento de performance |

## Etapas de construção

Cada etapa termina em algo **demonstrável e vendável**. Ver
[13 · Roadmap e riscos](13-ROADMAP-E-RISCOS.md) para o quadro geral.

| Etapa | Entrega | Detalhe |
|---|---|---|
| E1 | fundação: tenant, auth, RLS, catálogo, portfólio público | [detalhada](etapas/E1-FUNDACAO.md) |
| E2 | comercial: cliente, pedido, briefing, ficha de direção | [detalhada](etapas/E2-COMERCIAL.md) |
| E3 | dinheiro: PSP, KYC, split, sinal e saldo, conciliação | [esqueleto](etapas/E3-DINHEIRO.md) |
| E4 | entrega: upload, derivadas, galeria, seleção, ZIP | [esqueleto](etapas/E4-ENTREGA.md) |
| E5 | agenda e contrato: disponibilidade, assinatura, lembretes | [esqueleto](etapas/E5-AGENDA-E-CONTRATO.md) |

E3–E5 estão de propósito em nível de objetivo e critério de aceite. Detalhar
tarefa de E4 hoje é escrever ficção: a etapa começa depois de três meses de
aprendizado que ainda não aconteceu.

## Decisões registradas

[Índice de ADRs](adr/README.md) — 10 decisões, todas com contexto,
alternativas e consequência.

## Como manter isto vivo

1. Regra de negócio nova entra em [06](06-REGRAS-DE-NEGOCIO.md) com ID novo.
   Nunca renumere: ID é citado em nome de teste.
2. Decisão que fecha uma alternativa vira ADR. Decisão revogada não se apaga:
   marca-se `Substituída por ADR-00NN`.
3. O CI valida link interno quebrado, `RN` citada sem definição e ADR
   inexistente — `./scripts/check-docs.sh` roda igual na sua máquina.
