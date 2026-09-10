# 09 · Galerias e entrega

Módulo que mais consome infraestrutura e o que mais gera indicação. **Trate
custo de storage como requisito, não como detalhe** — é o único item do
produto cujo custo cresce sozinho, sem ninguém mexer.

---

## 1. Decisões

| Ponto | Decisão |
|---|---|
| Upload | direto do navegador para o storage, com URL pré-assinada e multipart. **O arquivo não passa pela API** |
| Derivadas | worker gera `thumb` 480, `web` 1600 com marca d'água, `texture` 1024 e `lqip` 20px. O **original fica intocado** |
| Entrega | só derivadas por padrão. Original liberado após `SelecaoFechada` **e** saldo pago |
| Seleção | limite de favoritas pelo pacote; excedente vira upsell que gera novo `Payment` |
| Compartilhamento | link assinado + senha + expiração. Convidado não cria conta |
| Download | ZIP montado em background, notificado por e-mail. **Nunca no request** |
| Ciclo de vida | galeria expira em N meses; aviso em 30/7/1 dia; depois vai para storage frio ou é arquivada. **Está no contrato** |
| Proteção | marca d'água + URL assinada curta. Bloquear clique direito é teatro — não gaste tempo |

## 2. Upload

```
 navegador                      API                       R2 / MinIO
    │                            │                            │
    │ POST /galleries/{id}/upload-url                          │
    │  { fileName, sizeBytes, contentType }                    │
    ├───────────────────────────►│                            │
    │                            │ autoriza (RN-ENT-004)      │
    │                            │ cria Photo em EmPreparo    │
    │                            │ assina PUT multipart       │
    │◄───────────────────────────┤                            │
    │  { photoId, uploadId, parts[{n, url}] }                  │
    │                                                          │
    │ PUT part 1..N  (paralelo, direto no storage)             │
    ├─────────────────────────────────────────────────────────►│
    │                                                          │
    │ POST /galleries/{id}/photos/{photoId}/complete           │
    ├───────────────────────────►│                            │
    │                            │ CompleteMultipartUpload    │
    │                            │ enfileira job de derivadas │
```

Por que assim:

- **Custo e latência.** Um casamento tem 800 fotos de 25 MB. Passar 20 GB pela
  API é banda paga duas vezes e um timeout garantido.
- **Retomada.** Multipart permite reenviar só a parte que falhou — em upload
  de 20 GB por 4G, isso é a diferença entre funcionar e não funcionar.
- **A API continua sendo a autoridade.** Ela autoriza antes de assinar
  ([RN-ENT-004](06-REGRAS-DE-NEGOCIO.md)) e só considera a foto existente
  depois do `complete`.

## 3. Derivadas

Geradas no worker, fora do request ([RN-ENT-011](06-REGRAS-DE-NEGOCIO.md)):

| Variante | Largura | Formato | Marca d'água | Uso |
|---|---|---|---|---|
| `original` | — | como recebido | **não** | arquivo-fonte. Nunca alterado, nunca sobrescrito |
| `thumb` | 480 px | WebP q80 | não | grade da galeria, seleção |
| `web` | 1600 px | WebP q80 | **sim** | visualização, proofing, galeria bloqueada |
| `texture` | 1024 px | WebP q80 | não | camada WebGL do portfólio. Ver [16](16-FRONTEND-E-EXPERIENCIA.md) |
| `lqip` | 20 px | base64 inline | não | placeholder embutido no HTML, evita CLS |

O `lqip` e a cor dominante são gerados no mesmo passe do ImageSharp. Custam
quase nada e resolvem duas coisas de uma vez: **CLS zero** no portfólio e
placeholder decente na galeria em conexão ruim. É o tipo de detalhe que
separa uma galeria que parece profissional de uma que parece lenta.

O `original` fica intocado ([RN-ENT-003](06-REGRAS-DE-NEGOCIO.md)): regerar
derivada é criar arquivo novo, nunca reprocessar em cima.

## 4. Entrega e liberação

| Situação | O cliente vê | Pode baixar |
|---|---|---|
| Galeria `EmPreparo` | nada — não aparece no portal | — |
| `Disponivel` | `thumb` + `web` com marca d'água | nada |
| `BloqueadaPorPendencia` | `thumb` + `web` com marca d'água, aviso de saldo | nada |
| `EmSelecao` | idem, com contador de favoritas | nada |
| `SelecaoFechada`, saldo pendente | idem | nada |
| `SelecaoFechada`, saldo `Confirmado` → `Entregue` | `web` sem marca d'água | **original** e ZIP |
| Convidado por `ShareLink` | `thumb` + `web` com marca d'água | nunca original |

[RN-ENT-021](06-REGRAS-DE-NEGOCIO.md).

### `BloqueadaPorPendencia` — a alavanca de cobrança

Saldo não pago: a galeria **abre**, em baixa resolução com marca d'água, e o
download em alta fica travado, com aviso claro do valor pendente e um botão de
pagamento.

É a alavanca de cobrança mais eficaz do produto e evita a conversa
constrangedora por WhatsApp. **Esconder a galeria seria pior:** gera ligação
de reclamação. Mostrar com marca d'água gera pagamento.
[RN-ENT-032](06-REGRAS-DE-NEGOCIO.md).

## 5. Seleção e upsell

```
pacote inclui 40 fotos editadas
cliente favorita 52
        │
        ├─ 40 dentro do limite
        └─ 12 excedentes ──► upsell: "+12 fotos por R$ 480"
                                   │
                                   └─► novo Payment
                                          │
                                          └─ Confirmado ──► SelecaoFechada permitida
```

- O limite vem do pacote, copiado para a galeria na criação.
- Excedente **não bloqueia a navegação** — bloqueia o fechamento da seleção.
  O cliente pode favoritar à vontade e decide no fim.
- O preço do excedente é configurável por pacote, por foto ou em bloco.
- A seleção só fecha com o upsell `Confirmado`
  ([RN-ENT-020](06-REGRAS-DE-NEGOCIO.md)).
- Job fecha a seleção no vencimento do prazo, com o que houver dentro do
  limite — galeria em `EmSelecao` eterna é galeria que nunca vira `Concluido`.

## 6. Compartilhamento com convidado

O principal canal de aquisição orgânica do produto. Cada casamento entrega o
link para dezenas de pessoas.

| Item | Regra |
|---|---|
| Formato | `GET /s/{token}` — token assinado, não sequencial, não adivinhável |
| Senha | mínimo 6 caracteres, definida pelo cliente ou pelo estúdio |
| Expiração | padrão 90 dias, configurável |
| Acesso | somente leitura. Favoritar é opcional e configurável |
| Download | **nunca** o original. Derivada `web` com marca d'água, se o estúdio permitir |
| Sessão | guardada em Redis com TTL, não em cookie permanente |
| Rate limit | 20 req/min por token — link vazado não vira scraping |
| Marca | assinatura discreta do **fotógrafo** e caminho para o portfólio dele. Nunca a marca do Prata em primeiro plano |

[RN-ENT-040](06-REGRAS-DE-NEGOCIO.md).

A última linha é decisão de produto: o convidado precisa sair sabendo o nome
do fotógrafo, não o nome da plataforma. Quem indica é ele.

## 7. Download em ZIP

| Item | Regra |
|---|---|
| Disparo | `POST` devolve `202` com `downloadJobId` ([RN-ENT-041](06-REGRAS-DE-NEGOCIO.md)) |
| Montagem | worker, em streaming para o storage de export. Nunca em memória |
| Notificação | e-mail com URL assinada quando pronto |
| Limite | 10 GB por ZIP; acima disso, divide em volumes |
| TTL | 48 h, depois o ZIP é apagado ([RN-ENT-042](06-REGRAS-DE-NEGOCIO.md)) |
| Idempotência | pedido repetido do mesmo escopo reaproveita o job em andamento |

ZIP no request morre: 20 GB não cabem num timeout de gateway, e o cliente
aperta F5 três vezes gerando três montagens.

## 8. Ciclo de vida e custo

### Política

| Fase | Quando | O que acontece |
|---|---|---|
| Ativa | 0 → N meses (padrão 12) | acesso normal, storage quente |
| Aviso | 30, 7 e 1 dia antes do fim | e-mail ao cliente e ao estúdio |
| `Expirada` | após N meses | acesso encerrado, objeto vai para storage frio |
| `Arquivada` | após a expiração | recuperável sob demanda pelo `tenant.owner`, com custo de reativação |

**O prazo consta no contrato** ([RN-ENT-050](06-REGRAS-DE-NEGOCIO.md),
[RN-CTR-002](06-REGRAS-DE-NEGOCIO.md)). Sem cláusula, a expectativa do cliente
é "para sempre" — e "para sempre" é uma conta que cresce todo mês.

### Ordem de grandeza do custo

Confirmar na tabela vigente do provedor; os números servem para dimensionar a
política, não para orçamento.

| Item | Estimativa |
|---|---|
| Casamento típico entregue | ~800 fotos · original ~25 MB · derivadas ~2 GB → **~22 GB** |
| Storage R2, por casamento/mês | ~US$ 0,33 |
| 100 casamentos retidos 12 meses | ~2,2 TB → **~US$ 33/mês** |
| Egress | **US$ 0,00** — é por isso que R2 foi escolhido |

O egress é o que mata o modelo em provedor tradicional: cada galeria
compartilhada com 30 convidados serve os mesmos gigabytes 30 vezes. Em R2
isso custa zero; em S3 padrão, mais que o storage. Ver
[ADR-0008](adr/ADR-0008-storage-r2-derivadas-no-worker.md).

Duas alavancas de custo, nesta ordem: **expiração contratual** e **storage
frio**. Compressão agressiva do original é a terceira, e a pior — o original
é o ativo do fotógrafo.

## 9. Proteção de imagem

| Medida | Vale a pena? |
|---|---|
| Marca d'água na derivada `web` | **sim** — é a proteção real na fase de proofing |
| URL assinada com TTL curto (15 min) | **sim** — link copiado expira |
| Bucket privado, sem exceção | **sim** ([RN-ENT-002](06-REGRAS-DE-NEGOCIO.md)) |
| Rate limit no `ShareLink` | **sim** — impede varredura |
| Bloquear clique direito | **não. É teatro.** Print de tela existe |
| Desabilitar arrastar imagem | não |
| Canvas para "esconder" a imagem | não — quebra SEO, acessibilidade e LCP, e não esconde nada |
| DRM / watermark invisível | não na v1. Complexidade alta, benefício nulo nesse mercado |

A régua: proteger contra **redistribuição casual e uso comercial indevido**,
não contra alguém determinado. Quem quer a foto tira print. O que a marca
d'água impede é a foto sem marca circulando como se fosse a entrega final.

## 10. Jobs deste módulo

| Job | Quando | O que faz |
|---|---|---|
| `GerarDerivadas` | evento `FotoEnviada` | thumb, web+marca, texture, lqip, cor dominante |
| `MontarZip` | sob demanda | streaming para o bucket de export |
| `LimparExports` | horário | apaga ZIP com TTL vencido |
| `AvisarExpiracao` | diário | e-mail em 30/7/1 dia |
| `ExpirarGalerias` | diário | `Expirada` → storage frio |
| `FecharSelecaoVencida` | diário | fecha seleção no prazo |
| `BloquearPorPendencia` | diário | `Disponivel` → `BloqueadaPorPendencia` |

Detalhe operacional e alarmes em
[14 · Ambientes e operação](14-AMBIENTES-E-OPERACAO.md).
