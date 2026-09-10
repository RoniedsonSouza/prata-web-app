# 07 · Briefing dirigido

O diferencial do produto. O mercado tem galeria e tem CRM; quase ninguém liga
**briefing de direção de pose** ao pedido.

A premissa: **a principal causa de insatisfação com um ensaio não é técnica —
é a pessoa não ter gostado de si mesma na foto.** Isso é previsível, e a única
forma de prever é perguntar antes.

A saída não é um formulário arquivado. É a **ficha de direção**: uma página em
PDF que o fotógrafo leva no bolso no dia. É o que transforma formulário em
valor percebido.

---

## 1. Princípios

| Princípio | Consequência de projeto |
|---|---|
| Ninguém responde 40 perguntas | Aplicação **condicional** por `ServiceType`. Casamento vê o bloco C inteiro; book corporativo não vê nada dele |
| O fotógrafo é o dono das perguntas | `BriefingTemplate` é por tenant, editável. A plataforma entrega semente, não lei |
| Pergunta sobre corpo é campo minado | Bloco B é opcional, com consentimento específico. Nunca se pergunta diagnóstico — só acomodação. Ver seção 5 |
| Resposta antiga não pode mudar | `Answer` guarda snapshot do texto da pergunta ([RN-BRF-002](06-REGRAS-DE-NEGOCIO.md)) |
| Formulário longo abandona | Autosave a cada campo, retomada por link, barra de progresso por bloco |
| Uma resposta vale mais que cinco escalas | "Qual foto sua você mais gosta e por quê?", com upload, é a pergunta mais útil do conjunto |

---

## 2. Banco de perguntas

Cinco blocos. A coluna **Tipo** é o `QuestionType`; **Sens.** marca
`IsSensitive`; **Serviços** indica onde a pergunta aparece por padrão.

Legenda de serviços: `CAS` casamento · `PRE` pré-wedding · `INF` aniversário
infantil · `STU` studio/book · `COR` corporativo · `GES` gestante ·
`NEW` newborn · `FOR` formatura · `FAM` família · `15A` book 15 anos ·
`TODOS`.

### Bloco A · Evento e logística

| # | Pergunta | Tipo | Serviços |
|---|---|---|---|
| A1 | Data e horário previstos | `Date` | TODOS |
| A2 | Cidade e local (com link do mapa, se houver) | `Text` | TODOS |
| A3 | Número aproximado de pessoas | `Number` | CAS · INF · FOR · FAM · COR |
| A4 | Ambiente: interno · externo · os dois · ainda não definido | `SingleChoice` | TODOS |
| A5 | Já existe cronograma do dia? | `SingleChoice` | CAS · FOR |
| A6 | Extras desejados: drone · segundo fotógrafo · vídeo · álbum impresso · making of · hora extra | `MultiChoice` | CAS · PRE · INF · FOR |
| A7 | Precisa de pausas durante o ensaio? | `SingleChoice` | GES · NEW · FAM · 15A |
| A8 | Alguma restrição de deslocamento no local? (evitar escadas, ficar muito tempo em pé, sol forte) | `Chips` + `Text` | TODOS |

> **A7 e A8 são a forma correta de perguntar acessibilidade.** Guardam a
> **acomodação**, nunca a causa. Ver seção 5 e
> [RN-BRF-021](06-REGRAS-DE-NEGOCIO.md).

### Bloco B · Direção e conforto na frente da câmera

O bloco que diferencia o produto. **Todo ele é opcional** e exige
consentimento específico ([RN-BRF-020](06-REGRAS-DE-NEGOCIO.md)).

| # | Pergunta | Tipo | Sens. | Serviços |
|---|---|---|---|---|
| B1 | Você gosta de sorrir nas fotos? `1` prefiro sério → `5` sorriso aberto sempre | `Scale` 1–5 | — | TODOS |
| B2 | Lado preferido do rosto: esquerdo · direito · não sei · tanto faz | `SingleChoice` | — | STU · COR · 15A · GES |
| B3 | Algo que prefere evitar nas imagens? | `Chips` + `Text` | **sim** | TODOS |
| B4 | Nível de conforto diante da câmera. `1` travo totalmente → `5` adoro posar | `Scale` 1–5 | — | TODOS |
| B5 | Prefere ser dirigido passo a passo ou fotografado espontaneamente? dirigido ↔ documental | `Scale` 1–5 | — | TODOS |
| B6 | Enquadramento preferido: corpo inteiro · meio corpo · close · confio no fotógrafo | `SingleChoice` | — | STU · COR · 15A · GES |
| B7 | Usa óculos? Prefere manter nas fotos? | `SingleChoice` | — | TODOS |
| B8 | Qual foto sua você mais gosta e por quê? | `Text` + `Upload` | — | TODOS |
| B9 | Tem alguma foto sua de que você não gostou? O que incomodou? | `Text` | **sim** | TODOS |
| B10 | Como prefere ser chamado(a) durante o ensaio? | `Text` | — | TODOS |

**Chips sugeridos em B3** — sempre com campo livre ao lado:

```
sorriso mostrando os dentes · ângulo de baixo · perfil · braços · barriga
topo da cabeça · reflexo no óculos · aparelho ortodôntico · tatuagem
cicatriz · marca de nascença
```

Por que cada uma importa na condução:

| Resposta | O que o fotógrafo faz com ela |
|---|---|
| B1 baixo | Para de pedir sorriso. Conduz por expressão contida — quem odeia sorrir e é forçado entrega o ensaio inteiro travado |
| B2 "não sei" | O formulário oferece um teste rápido: duas selfies, uma de cada lado, e o cliente escolhe |
| B4 abaixo de 3 | Reserva **20 minutos de aquecimento** antes das fotos que importam |
| B5 documental | Menos comando, mais espera. Lente mais longa |
| B8 com upload | Uma referência da própria pessoa vale mais que cinco escalas. É o item mais citado na ficha de direção |
| B10 | Chamar pelo apelido certo muda a resposta na primeira foto |

### Bloco C · Pessoas e história

| # | Pergunta | Tipo | Sens. | Serviços |
|---|---|---|---|---|
| C1 | Fotos obrigatórias — lista nominal (avós, padrinhos, grupo do trabalho…) | `List` | — | CAS · INF · FOR · FAM |
| C2 | Alguém que exige atenção especial? (pessoa idosa, criança pequena, alguém que sai cedo) | `Text` | **sim** | CAS · INF · FOR · FAM |
| C3 | Há pessoas que não devem aparecer juntas na mesma foto? | `Text` | **sim** | CAS · INF · FAM |
| C4 | Alguém não autoriza uso de imagem? | `MultiChoice` + `Text` | **sim** | CAS · INF · FOR · FAM |
| C5 | Animal de estimação participa? | `Text` | — | PRE · FAM · NEW · GES |

> **C3 é campo discreto**, visível apenas para o fotógrafo e a equipe
> designada, nunca impresso em documento que circule entre convidados. Evita
> o constrangimento no dia — e é exatamente o tipo de informação que hoje se
> perde numa conversa de WhatsApp de três meses antes.

> **C4 exige atenção redobrada com crianças.** A resposta se cruza com
> [RN-ENT-031](06-REGRAS-DE-NEGOCIO.md): havendo menor identificado, a galeria
> nasce bloqueada para uso em portfólio, independente de qualquer outra
> resposta.

### Bloco D · Estética e entrega

| # | Pergunta | Tipo | Serviços |
|---|---|---|---|
| D1 | Estilo de edição: claro e aéreo · contrastado · fílmico · preto e branco · confio no fotógrafo | `SingleChoice` | TODOS |
| D2 | Referências (Pinterest, Instagram, prints) | `Url` + `Upload` | TODOS |
| D3 | Roupas e cores planejadas | `Text` | TODOS |
| D4 | **Autoriza uso das imagens no portfólio e redes?** Sim · Somente sem rosto · Não | `SingleChoice` | TODOS |
| D5 | Prazo esperado de entrega, se houver | `SingleChoice` | TODOS |

> **D4 é a pergunta com maior consequência de sistema.** Vira cláusula do
> contrato e flag `PortfolioConsent` na galeria. Sem ela, o fotógrafo não sabe
> o que pode postar — e postar o que não podia é processo. Ver
> [RN-VIT-005](06-REGRAS-DE-NEGOCIO.md) e
> [RN-ENT-030](06-REGRAS-DE-NEGOCIO.md).

### Bloco E · Contato e comercial

| # | Pergunta | Tipo | Serviços |
|---|---|---|---|
| E1 | Canal e horário preferidos de contato: WhatsApp · e-mail · ligação | `SingleChoice` | TODOS |
| E2 | Faixa de investimento pretendida | `SingleChoice` | TODOS |
| E3 | Forma de pagamento preferida: Pix à vista · cartão parcelado · Pix parcelado | `SingleChoice` | TODOS |
| E4 | Como nos encontrou? | `SingleChoice` + `Text` | TODOS |

> **E2 qualifica o lead antes de o fotógrafo gastar uma hora numa proposta.**
> É a pergunta que mais economiza tempo do estúdio, e a que mais gera
> resistência de preenchimento — por isso é `SingleChoice` em faixas, nunca
> campo aberto de valor.

---

## 3. Modelagem

```
BriefingTemplate                       Answer  (por Order)
  ├─ TenantId                            ├─ OrderId
  ├─ ServiceTypeId                       ├─ QuestionId
  ├─ Version                             ├─ QuestionTextSnapshot   ← RN-BRF-002
  ├─ IsPublished                         ├─ QuestionTypeSnapshot
  └─ Questions[]                         ├─ Value : jsonb          ← RN-BRF-004
       ├─ Code            (A1, B3…)      ├─ IsSensitive            ← cópia, não join
       ├─ Text                           ├─ AnsweredAt
       ├─ HelpText                       └─ ConsentId?             ← se sensível
       ├─ QuestionType
       ├─ IsRequired
       ├─ IsSensitive
       ├─ SortOrder
       ├─ VisibleWhen     (expressão)
       └─ Options[]  (Code, Label, SortOrder, IsSuggestedChip)
```

### `QuestionType`

| Tipo | Forma no `jsonb` | Uso |
|---|---|---|
| `Text` | `{"text": "..."}` | resposta livre |
| `SingleChoice` | `{"option": "CODE"}` | escolha única |
| `MultiChoice` | `{"options": ["A","B"]}` | múltipla |
| `Chips` | `{"chips": ["A"], "text": "..."}` | sugestões clicáveis **mais** campo livre |
| `Scale` | `{"value": 3, "min": 1, "max": 5}` | escala com rótulo nas pontas |
| `Date` | `{"date": "2027-05-14", "time": "16:00"}` | data e hora |
| `Number` | `{"number": 120}` | contagem |
| `Url` | `{"urls": ["https://..."]}` | referências |
| `Upload` | `{"assets": [{"key": "...", "name": "..."}]}` | arquivo no storage, nunca no banco |
| `List` | `{"items": ["avós", "padrinhos"]}` | lista nominal |

### `VisibleWhen`

Expressão simples, avaliada no cliente **e** revalidada no servidor. Só
referencia pergunta anterior no mesmo template
([RN-BRF-003](06-REGRAS-DE-NEGOCIO.md)):

```
B2 visível quando  B1.value >= 3
B3 visível sempre
C4 visível quando  A3.number > 1
D4 visível sempre
```

### Por que `jsonb` e não EAV

Uma tabela `answer_value(question_id, field_name, string_value, int_value,
date_value…)` é o caminho natural e é uma armadilha: toda leitura de briefing
vira dez `JOIN`, todo tipo novo vira coluna nova, e a validação sai do
domínio. Com `jsonb` a resposta é um documento tipado pelo `QuestionType`,
indexável por `GIN` quando precisar, e o domínio valida a forma na entrada.
Ver [11 · Modelo de dados](11-MODELO-DE-DADOS.md).

---

## 4. Templates semente

Ao criar um tenant, a plataforma **copia** os templates semente para ele
([RN-BRF-001](06-REGRAS-DE-NEGOCIO.md)). Cópia, não referência: o fotógrafo
edita as perguntas dele sem afetar ninguém, e uma melhoria na semente não
reescreve briefing de quem já está rodando.

Composição sugerida por serviço:

| Serviço | Blocos | ~nº de perguntas |
|---|---|---|
| Casamento | A completo · B completo · C completo · D · E | 30 |
| Pré-wedding | A1–A4, A6 · B completo · C5 · D · E | 22 |
| Gestante | A1–A4, A7, A8 · B completo · C5 · D · E | 23 |
| Newborn | A1, A2, A4, A7 · B1, B4, B8, B10 · C5 · D · E | 17 |
| Aniversário infantil | A completo · B1, B4, B10 · C1–C4 · D · E | 22 |
| Book 15 anos | A1–A4 · B completo · C1, C4 · D · E | 24 |
| Studio / book | A1, A2, A4 · B completo · D · E | 19 |
| Corporativo | A1–A4 · B1, B2, B4, B6, B7 · D1, D3, D4 · E1, E4 | 15 |
| Formatura | A completo · B1, B4 · C1, C2, C4 · D · E | 21 |
| Família | A1–A4, A7, A8 · B1, B4, B5, B8, B10 · C1–C5 · D · E | 25 |

---

## 5. LGPD — leia antes de modelar o bloco B

Pergunta sobre corpo, mobilidade ou condição de saúde produz **dado pessoal
sensível** (LGPD, art. 5º, II e art. 11). Três regras resolvem:

### 5.1 Não pergunte diagnóstico. Pergunte a acomodação.

| Não pergunte | Pergunte |
|---|---|
| "Tem alguma deficiência?" | "Precisa de pausas durante o ensaio?" |
| "Tem problema de mobilidade?" | "Evitar escadas ou ficar muito tempo em pé?" |
| "Tem alguma condição de saúde?" | "Evitar sol forte ou calor?" |
| "Está grávida de quantas semanas?" (fora de ensaio gestante) | "Há alguma limitação de tempo para o ensaio?" |
| "Tem alguma cicatriz ou marca?" | B3, como preferência estética, com campo livre e opcional |

Guarda-se a **acomodação operacional**, nunca a causa. O fotógrafo precisa
saber que tem que reservar uma cadeira — não precisa saber por quê.

### 5.2 Consentimento específico

O bloco B inteiro é opcional, com consentimento **separado** do aceite do
contrato, revogável, e com finalidade declarada em texto simples ao lado do
campo — não em link para política de 12 páginas
([RN-BRF-020](06-REGRAS-DE-NEGOCIO.md),
[RN-LGP-003](06-REGRAS-DE-NEGOCIO.md)).

Texto sugerido, ao lado do bloco:

> Estas perguntas são opcionais e servem só para o fotógrafo conduzir melhor o
> seu ensaio. Só quem vai fotografar você tem acesso. Você pode deixar em
> branco ou apagar depois, sem prejuízo nenhum ao seu pedido.

### 5.3 Visibilidade e retenção

| Regra | Detalhe |
|---|---|
| Quem vê | `tenant.owner` e `tenant.staff` **explicitamente designado no pedido**. Nunca `platform.admin` ([RN-BRF-030](06-REGRAS-DE-NEGOCIO.md)) |
| Onde nunca aparece | log, trace, métrica, mensagem de erro, e-mail, notificação ([RN-BRF-031](06-REGRAS-DE-NEGOCIO.md), [RN-LGP-005](06-REGRAS-DE-NEGOCIO.md)) |
| Retenção | apagada por job **12 meses após a entrega**, com registro em auditoria ([RN-LGP-004](06-REGRAS-DE-NEGOCIO.md)) |
| Revogação | apaga as respostas sensíveis e mantém o pedido íntegro |
| Auditoria | **leitura** de resposta sensível gera registro em `AuditLog` ([RN-AUD-003](06-REGRAS-DE-NEGOCIO.md)) |

Vale registrar o incentivo torto que essas regras evitam: sem retenção
definida, a base acumula por anos o dado mais sensível do produto sem nenhum
uso — e passa a ser só risco.

---

## 6. Ficha de direção — a saída que importa

Uma página A4, gerada sob demanda, servida por URL assinada de TTL curto.
Nunca anexada em e-mail ([RN-BRF-040](06-REGRAS-DE-NEGOCIO.md)).

Layout proposto:

```
┌──────────────────────────────────────────────────────────────────┐
│  MARIA E JOÃO · Casamento · 14/05/2027 · 16h                     │
│  Espaço Villa Bella, Vila Velha/ES          [ mapa ]  [ QR ]     │
├───────────────────────────────┬──────────────────────────────────┤
│  COMO CONDUZIR                │  REFERÊNCIA DA PRÓPRIA PESSOA    │
│  Sorriso        ●●○○○  contido│  ┌────────────┐  "gosto porque   │
│  Conforto       ●●○○○  travada│  │  upload B8  │   o rosto está   │
│  Direção        dirigido      │  └────────────┘   de lado"       │
│  Enquadramento  meio corpo    │                                  │
│  Chamar de      "Mari"        │  EVITAR                          │
│  Óculos         manter        │  · ângulo de baixo               │
│                               │  · braços                        │
│  ⚠ Reservar 20 min de         │  · reflexo no óculos             │
│    aquecimento                │                                  │
├───────────────────────────────┴──────────────────────────────────┤
│  FOTOS OBRIGATÓRIAS                     ATENÇÃO                  │
│  □ avós maternos                        · avó sai às 19h         │
│  □ padrinhos (6)                        · não juntar tio Carlos  │
│  □ grupo do trabalho da noiva             e tia Rita             │
│  □ pet (Nina, chega às 17h)             · Pedro (12) sem uso     │
│                                           de imagem              │
├──────────────────────────────────────────────────────────────────┤
│  ENTREGA   fílmico · sem rosto no portfólio · prazo 30 dias      │
└──────────────────────────────────────────────────────────────────┘
```

Decisões de conteúdo:

- **Cabe em uma página, sempre.** Briefing de 30 perguntas gera a mesma
  página; o que não cabe é cortado por prioridade, não por truncamento.
- As escalas viram **bolinhas**, não números. `●●○○○` se lê de longe, num
  salão escuro, com a câmera na mão.
- O alerta de aquecimento é **derivado** de B4 < 3, não digitado.
- O bloco ATENÇÃO junta C2, C3 e C4 — a informação que causa constrangimento
  se esquecida.
- A ficha **não** imprime a causa de nada. Imprime a acomodação.
- Nome do arquivo sem dado pessoal: `ficha-{orderId}.pdf`, nunca
  `ficha-maria-joao.pdf` — o nome do arquivo vaza no histórico de download e
  no cache do dispositivo.
