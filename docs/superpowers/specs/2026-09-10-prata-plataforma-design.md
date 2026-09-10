# Prata · registro do design

**Data:** 2026-09-10
**Caminho:** arquitetural (projeto novo)
**Situação:** aprovado; documentação escrita

Registro do que foi decidido nesta conversa de design e por quê. É um
instantâneo, não documentação viva — a referência que se mantém está em
[`docs/`](../../README.md).

---

## 1. Ponto de partida

O escopo técnico v0.1 chegou pronto, em treze seções, descrevendo um **SaaS
vertical multi-tenant de gestão e entrega fotográfica**. O trabalho aqui não
foi descobrir o produto: foi **fechar as quatro decisões que o próprio escopo
marcava como pendentes**, resolver três incoerências internas e materializar
tudo em documentação executável em etapas.

Pedido explícito: **só documentação e configuração**. Nenhuma página,
componente ou entidade.

## 2. Decisões fechadas nesta conversa

As quatro que o escopo listava como "a decidir antes de escrever código".
Todas foram decididas na recomendação apresentada.

| Decisão | Escolha | Motivo curto | ADR |
|---|---|---|---|
| Modelo de receita | **comissão por transação**, sem assinatura na v1 | barreira de entrada zero; evita construir billing próprio da plataforma antes de provar valor | [0003](../../adr/ADR-0003-modelo-de-receita-comissao.md) |
| Domínio do fotógrafo | **só subdomínio** na v1; próprio na E5 | wildcard resolve a E1 inteira; subdomínio indexa; evita ticket de DNS na fase em que cada hora conta | [0004](../../adr/ADR-0004-subdominio-na-v1.md) |
| Assinatura de contrato | **aceite próprio** (hash + IP + UA + timestamp) com port trocável | Lei 14.063/2020 art. 4º basta entre as partes; custo zero; ZapSign entra sem tocar o domínio | [0006](../../adr/ADR-0006-assinatura-eletronica-propria.md) |
| WhatsApp | **`wa.me`** na v1; Cloud API opcional na E5 | em multi-tenant a Cloud API exige um WABA por fotógrafo — ou mandar do número da plataforma, o que confunde o cliente final | [0007](../../adr/ADR-0007-whatsapp-wame-na-v1.md) |

### Decisão adicional, provocada pela verificação de front-end

O usuário pediu que o portfólio público tivesse layout e movimento em nível de
estúdio premiado, com efeito de scroll e WebGL.

**Tensão identificada:** o portfólio é onde o SEO acontece e onde o egress
custa; e o produto não é uma peça sob medida, é um **template que N fotógrafos
usam com fotos que ninguém do time escolheu**.

**Resolução:** não se constrói um site premiado — constrói-se um **motor de
temas** de nível premiado, com a técnica **DOM primeiro, canvas sobreposto**.

| | Escolha |
|---|---|
| Motion | GSAP + ScrollTrigger · Lenis · Motion (`motion/react`) · View Transitions |
| WebGL | R3F + Three.js + GLSL, em chunk isolado após o LCP; OGL como escape hatch |
| **Recusado** | **PixiJS** — segundo renderer e segundo contexto WebGL, zero ganho num portfólio de dezenas de fotos |
| **Recusado** | galeria construída dentro do canvas — custa SEO, acessibilidade, LCP e a degradação |
| Temas | três curados: `editorial` (E1), `cinema` e `imersivo` (E4) |
| Controle | orçamento como **gate de CI**, não aspiração |

Registrada em [ADR-0010](../../adr/ADR-0010-motion-e-webgl.md) e detalhada em
[16 · Front-end](../../16-FRONTEND-E-EXPERIENCIA.md).

## 3. Correções ao escopo de origem

Seis, todas verificadas antes de escrever.

| # | O escopo dizia | Correção | Onde |
|---|---|---|---|
| 1 | Next.js 15 | **Next.js 16** (`16.3.4`); o 15 está em backport. Mesma lógica que levou o backend a .NET 10 LTS | [16, §1](../../16-FRONTEND-E-EXPERIENCIA.md) |
| 2 | MediatR implícito no padrão Clean Architecture | **pago desde a v13** → dispatcher próprio, ~40 linhas | [ADR-0001](../../adr/ADR-0001-dotnet-10-clean-architecture.md) |
| 3 | (não mencionava) FluentAssertions | **pago desde a v8** → `AwesomeAssertions`, fork Apache-2.0 | [15](../../15-ESTRATEGIA-DE-TESTES.md) |
| 4 | `Confirmado → Agendado`, e `Confirmado` reservando a data | contradição: Agenda é E5, `Confirmado` é E3. **`Confirmado` reserva a data**; `Agendado` é detalhe logístico e só ganha função na E5 | [05, §2](../../05-MAQUINAS-DE-ESTADO.md) |
| 5 | uma máquina de pagamento única | `Autorizado` não existe em Pix; `Liquidado` é D+0 no Pix e D+30 no cartão → **máquina por meio de pagamento** | [05, §3](../../05-MAQUINAS-DE-ESTADO.md) |
| 6 | KYC do fotógrafo como dependência da E3 | falta a outra: **a plataforma** precisa ser habilitada como marketplace no PSP — semanas de análise, sem código → tarefa da **E1** | [E1, §2](../../etapas/E1-FUNDACAO.md) |

## 4. Verificação empírica

O que foi testado, e não apenas afirmado:

| Verificação | Resultado |
|---|---|
| 46 versões de pacote NuGet | consultadas na API do NuGet; duas do manifest de ferramentas estavam erradas e foram corrigidas |
| 35 versões npm | consultadas no registro; revelaram Next 16 e TypeScript 7 como linha atual |
| Tags de imagem Docker | conferidas no Docker Hub; `docker compose config` valida |
| `.github/workflows/ci.yml` | `actionlint` limpo (achou dois avisos de shellcheck, corrigidos) |
| `scripts/db-roles.sql` | executado contra PostgreSQL 17 real, duas vezes — idempotente |
| **Padrão de RLS** | testado com seis casos contra Postgres real |

### O achado que corrigiu a documentação

O caso 6 do teste de RLS: `prata_owner` continuou vendo as linhas dos dois
tenants **mesmo com `FORCE ROW LEVEL SECURITY`**.

Motivo: `FORCE` aplica a policy ao **dono** da tabela, mas **superusuário
ignora RLS incondicionalmente** — e a imagem do Postgres cria o
`POSTGRES_USER` como superusuário. A primeira versão do documento dizia que
`FORCE` "vale até para o dono", o que só é verdade quando o dono não é
superusuário.

Consequência registrada em [03](../../03-MULTI-TENANCY.md),
[11](../../11-MODELO-DE-DADOS.md), no `docker-compose.yml` e num aviso dentro
do próprio `db-roles.sql`: **`prata_owner` não pode ser superusuário em
produção**, e o teste de isolamento roda sempre com `prata_app`.

Os outros cinco casos passaram como documentado — inclusive o mais importante
e menos óbvio: **sem a variável de sessão, nenhuma linha passa**, porque a
comparação com `NULL` resulta em `NULL`. Esquecer de setar o tenant retorna
vazio, nunca tudo.

## 5. Entregável

| Grupo | Conteúdo |
|---|---|
| Referência | 17 documentos numerados, `00` a `16` |
| Decisões | 10 ADRs, cada um com alternativas recusadas e consequências ruins |
| Regras | **123 regras `RN-XXX-000`** numeradas, com "como testar" |
| Etapas | E1 e E2 em nível de tarefa; E3–E5 em objetivo, critério de aceite e riscos |
| Configuração | `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `dotnet-tools.json`, `docker-compose.yml`, `.env.example`, CI, orçamento do Lighthouse |
| Scripts | `check-docs.sh` (roda igual no CI), `db-roles.sql` |

**Deliberadamente ausentes:** `.sln`, `web/package.json`, migration, entidade,
endpoint, página, componente. Criar solution vazia ou `package.json` já seria
começar o projeto; os comandos exatos estão escritos na
[E1](../../etapas/E1-FUNDACAO.md).

### Por que E3–E5 ficaram em esqueleto

O próprio escopo v0.1 diz que os estados do pedido e o modelo de receita são o
que mais muda depois de conversar com dois ou três fotógrafos reais. Detalhar
tarefa da E4 hoje, a três meses de distância e antes desse aprendizado, seria
escrever ficção. Objetivo, escopo, critério de aceite e riscos estão fechados;
a lista de tarefas se detalha quando a etapa começar.

## 6. O que continua aberto

Nenhum item bloqueia a E1.

| Aberto | Decidir em |
|---|---|
| `Booking` mínimo já na E3? (~2 dias; **recomendação: fazer**) | E3 |
| Emissão de nota fiscal da comissão — precisa de contador | antes de faturar |
| Enquadramento controlador/operador no termo de uso — precisa de advogado | antes de publicar |
| Preço do excedente de seleção: por foto ou em bloco | E4 |
| Storage frio: R2 IA ou Backblaze B2 — decidir com custo medido | E4 |
| Assinatura mensal como segundo modelo de receita | v2 |

Lista completa e as perguntas a fazer aos fotógrafos:
[13 · Roadmap e riscos](../../13-ROADMAP-E-RISCOS.md), seções 5 e 6.

## 7. Próximo passo

Executar a [E1 · Fundação](../../etapas/E1-FUNDACAO.md), começando pelas três
tarefas que não são de programação — em especial **solicitar a habilitação de
split no PSP**, que leva semanas e trava a E3 se ficar para depois.
