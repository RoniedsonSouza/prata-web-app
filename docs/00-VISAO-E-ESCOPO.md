# 00 · Visão e escopo

> **SaaS vertical multi-tenant de gestão e entrega fotográfica, com portfólio
> público, portal do cliente e split de pagamento.**

Use essa frase no README, no pitch e na landing. Ela é o produto.

---

## 1. O problema

Um fotógrafo autônomo hoje opera com quatro ferramentas desconectadas:
Instagram como portfólio, WhatsApp como CRM, planilha como financeiro e
Google Drive ou WeTransfer como entrega. O custo disso não é a assinatura —
é o retrabalho e o que se perde no meio: lead que esfria porque a proposta
demorou, data que foi prometida duas vezes, saldo que ninguém cobrou, link de
entrega que expirou sem ninguém baixar.

E existe um problema que nenhuma dessas ferramentas endereça: **a principal
causa de insatisfação com um ensaio não é técnica, é a pessoa não ter gostado
de si mesma na foto.** Isso é previsível e evitável — mas só se alguém
perguntar antes. Ninguém pergunta.

## 2. As quatro camadas do produto

Cada camada tem nome de mercado próprio. Saber os nomes é como se acha
concorrente, referência de UX e biblioteca pronta.

| Camada | Nome de mercado | Referências |
|---|---|---|
| Produto (o todo) | Vertical SaaS multi-tenant | HoneyBook, Táve |
| Ambiente do profissional | Studio Management Software (CRM + orçamento + agenda + financeiro) | Studio Ninja, Sprout Studio |
| Ambiente do cliente | Client Portal + Client Gallery / Proofing Platform | Pixieset, ShootProof, Pic-Time |
| Página pública | Portfolio site gerado pela plataforma | Format, Flothemes |
| Camada de dinheiro | Marketplace com split e subcontas de recebimento | Asaas, Pagar.me |

## 3. O diferencial

O mercado é maduro e está partido em dois: **ou é galeria de entrega, ou é
CRM.** Praticamente ninguém liga **briefing de direção de pose** ao pedido.

É onde está a diferenciação real do Prata, e é onde a energia de produto deve
ir. O caminho contrário — tentar ganhar da Pixieset em galeria — é perder
tempo e dinheiro: elas têm anos de vantagem em CDN, app e proofing.

Duas consequências práticas:

- O [briefing](07-BRIEFING.md) não é um formulário de cadastro. É o produto.
  A saída dele é a **ficha de direção**: uma página em PDF que o fotógrafo
  leva no bolso no dia do evento. É o que transforma formulário em valor
  percebido.
- A galeria precisa ser **boa e barata**, não premiada. Ela existe para
  fechar o ciclo e para gerar indicação.

## 4. Atores e superfícies

Cinco papéis, três superfícies. O erro clássico é tratar "fotógrafo" e
"equipe" como o mesmo usuário — **o segundo fotógrafo não pode ver o
financeiro.**

| Ator | Superfície | Pode | Role |
|---|---|---|---|
| Operador da plataforma | Console interno | onboarding de tenants, comissão, suporte, auditoria, métricas | `platform.admin` |
| Fotógrafo (dono do estúdio) | Back-office | tudo do próprio tenant: portfólio, serviços, preços, pedidos, financeiro, conta de repasse | `tenant.owner` |
| Equipe (2º fotógrafo, editor) | Back-office | agenda, galerias, entrega. **Sem financeiro e sem dados de repasse** | `tenant.staff` |
| Cliente final | Portal do cliente | pedido, briefing, contrato, pagamento, galeria, seleção, download | `client` |
| Visitante | Portfólio público | ver portfólio e coleções, iniciar cadastro | `anon` |

A matriz completa de permissão por recurso está em
[12 · Segurança e LGPD](12-SEGURANCA-E-LGPD.md).

### Convidado da galeria

Caso à parte, e vale desde a v1. O cliente compartilha o álbum com família e
padrinhos por **link assinado + senha, sem conta**. Ganha leitura e,
opcionalmente, favoritar.

Não é um detalhe de conveniência: **é o principal canal de aquisição orgânica
do produto.** Cada casamento entrega esse link para dezenas de pessoas, e boa
parte delas vai casar, ter filho ou fazer formatura nos próximos anos. Por
isso a página de galeria compartilhada carrega discretamente a assinatura do
fotógrafo e um caminho para o portfólio dele — nunca a marca do Prata em
primeiro plano. Ver [09 · Galerias](09-GALERIAS-E-ENTREGA.md).

## 5. Modelo de receita

**Comissão por transação**, via split do PSP. A plataforma fica com um
percentual configurável por tenant de cada cobrança liquidada, e o dinheiro
nunca passa pela conta da plataforma.

Sem mensalidade na v1. O motivo é de aquisição: um fotógrafo que ainda não
faturou nada pelo Prata não tem por que pagar assinatura, e cobrar antes de
provar valor mata o primeiro cliente. Assinatura entra na v2, quando houver
prova. Ver [ADR-0003](adr/ADR-0003-modelo-de-receita-comissao.md).

Consequência importante: **a plataforma só ganha dinheiro na E3.** E1 e E2
são investimento. Isso é escolha consciente, não descuido — mas exige que a
E3 não escorregue.

## 6. Escopo negativo — o que o Prata **não** é

Escopo negativo é o que impede a v1 de virar v3. Cada item abaixo já foi
considerado e recusado para esta versão.

| Não é | Por quê |
|---|---|
| Editor de fotos | Lightroom e Capture One resolvem. Nada de ajuste de imagem na plataforma |
| Rede social de fotógrafos | Feed, seguidores e curtida não vendem ensaio |
| Marketplace de busca de fotógrafo | Muda o produto de SaaS para intermediação, e coloca a plataforma competindo com o próprio cliente |
| App móvel nativo | Web responsiva resolve na v1. App só se a seleção de fotos pelo cliente provar atrito real |
| Instituição de pagamento | Nunca receber e repassar. Só split via PSP autorizado. Ver [08](08-PAGAMENTOS-SPLIT.md) |
| Emissor de nota fiscal | Integração possível na v2. Emitir NF por conta do fotógrafo é responsabilidade fiscal que não queremos na v1 |
| Assinatura com validade ICP-Brasil | Assinatura eletrônica simples resolve. Ver [ADR-0006](adr/ADR-0006-assinatura-eletronica-propria.md) |
| Site builder livre (drag and drop) | Três temas curados batem qualquer construtor livre em resultado visual. Ver [16](16-FRONTEND-E-EXPERIENCIA.md) |
| Multi-idioma / multi-moeda | Produto brasileiro, BRL, pt-BR. Internacionalizar depois custa menos que carregar o peso agora |
| Álbum impresso / print lab | Parceria, não produto |

## 7. Como se mede sucesso

Métricas por etapa. Se a etapa termina e a métrica não se move, o problema é
de produto, não de execução.

| Etapa | Métrica que importa | Alvo mínimo |
|---|---|---|
| E1 | fotógrafo real usando o portfólio como site oficial dele | 1 |
| E2 | pedidos que entram pelo portal em vez do WhatsApp | > 50% dos leads do tenant piloto |
| E3 | volume transacionado com split liquidado | primeira comissão recebida |
| E4 | convidados únicos por galeria compartilhada | > 15 por casamento |
| E5 | tenants ativos com agenda e contrato em uso | 5 |

Métrica que **não** vamos perseguir na v1: número de tenants cadastrados.
Tenant cadastrado que não transaciona é custo de storage.

## 8. Restrição de contexto

Um desenvolvedor, meio período, com trabalho paralelo. Isso não é detalhe de
cronograma — é restrição de arquitetura:

- **Nada de microsserviço.** Um monólito modular em Clean Architecture, um
  banco, um worker. Ver [02 · Arquitetura](02-ARQUITETURA.md).
- **Nada de infra que precise de plantão.** Serviço gerenciado sempre que o
  custo permitir.
- **Todo módulo entra com teste de domínio.** Não por rigor moral: porque não
  existe QA, e regressão em máquina de estados de dinheiro é caro.

A ordem realista das cinco etapas é de **5 a 7 meses**. O roadmap existe
justamente para ter algo na mão ao fim de cada uma, em vez de seis meses sem
nada. Ver [13 · Roadmap e riscos](13-ROADMAP-E-RISCOS.md).
