# ADR-0010 · GSAP + Lenis + R3F; PixiJS recusado

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E1 (motion) · E4 (WebGL)

## Contexto

O portfólio público precisa de layout e movimento em nível de estúdio de design
premiado — efeito de scroll, distorção de imagem, transição elaborada. E o
portfólio é, ao mesmo tempo, **onde o SEO do fotógrafo acontece** e o que
consome o egress que é o risco de custo nº 1 do produto.

Há um terceiro fato, específico: um site de agência premiada é peça sob medida,
com curadoria manual de conteúdo. Aqui é um **template que N fotógrafos usam
com fotos que ninguém do time escolheu.**

## Decisão

### Camadas

| Camada | Ferramenta | Etapa |
|---|---|---|
| Base | Next 16 · Tailwind v4 · `next/image` · `lqip` do worker | E1 |
| Motion | **GSAP + ScrollTrigger** · **Lenis** · **Motion** (`motion/react`) · View Transitions API | E1 |
| WebGL | **R3F + Three.js** · GLSL · `@react-three/postprocessing` | E4 |
| Escape hatch | **OGL** (~10 kb), se o chunk do R3F estourar o orçamento | — |

### A técnica que torna isso possível

**DOM primeiro, canvas sobreposto.** As fotos são `<img>` reais, indexáveis e
elegíveis a LCP; o canvas WebGL monta **depois do LCP**, sobreposto, lê a
posição de cada imagem por `getBoundingClientRect()` e só então a `<img>` vai
para `opacity: 0`. Falha em qualquer passo → volta ao DOM, em silêncio.
Detalhe em [16 · Front-end](../16-FRONTEND-E-EXPERIENCIA.md), seção 5.

### Motor de temas

Três temas curados (`editorial`, `cinema`, `imersivo`), não construtor livre.
Tenant escolhe tema + paleta + par tipográfico.

### Orçamento como gate de CI

120 kB de JS na rota pública sem WebGL · 200 kB no chunk WebGL · LCP ≤ 2,0 s ·
INP ≤ 200 ms · CLS ≤ 0,05 · SEO 100. Lighthouse CI e size-limit **quebram o
build** ([RN-FRT-001](../06-REGRAS-DE-NEGOCIO.md),
[RN-FRT-002](../06-REGRAS-DE-NEGOCIO.md)).

## Alternativas consideradas

| Alternativa | Decisão |
|---|---|
| **PixiJS** para efeito 2D | **recusado.** Sobrepõe 100% com o Three: seria um segundo renderer e um segundo contexto WebGL no mesmo bundle. Pixi ganha em milhares de sprites; um portfólio tem dezenas de fotos. Campo de partícula sai de `<Points>` com instancing no próprio R3F |
| **OGL** como principal (~10 kb vs ~150 kb do Three) | não como principal. Para shader de imagem isolado seria melhor, mas perde ecossistema (`drei`, `postprocessing`) e composição declarativa — que importam num template multi-tenant que precisa ser mantido por uma pessoa. **Mantido como escape hatch** |
| **ScrollSmoother** (GSAP) em vez de Lenis | Lenis é mais leve (~3 kb) e agnóstico. ScrollSmoother ficou gratuito e é boa opção, mas amarra a camada de scroll ao GSAP. Trocar é meia hora — está isolado num provider |
| **Locomotive Scroll** | a v5 é hoje essencialmente um wrapper do Lenis. Usar o Lenis direto |
| **Babylon.js** | peso de game engine para efeito de imagem |
| Galeria construída **dentro** do canvas | **recusado.** Entrega o mesmo efeito visual e custa SEO, acessibilidade, LCP e a possibilidade de degradar |
| Construtor livre de site (drag and drop) | recusado. Três temas curados batem qualquer construtor em resultado visual, e é escopo negativo declarado |
| Só CSS, sem GSAP | transição nativa não coreografa sequência sincronizada com scroll. GSAP resolve, é gratuito e é o padrão da indústria |

## Consequências

### Boas

- Efeito de nível premiado **sem** custar SEO, LCP ou acessibilidade.
- Degradação silenciosa: quem não passa na detecção de capacidade vê uma
  página correta, não uma quebrada.
- Motor de temas vira **diferencial vendável por tenant**, em vez de custo
  fixo de manutenção.
- **GSAP e todos os plugins ficaram gratuitos** após a aquisição pela Webflow
  — `ScrollTrigger`, `SplitText`, `ScrollSmoother` e `MorphSVG` inclusos, que
  antes eram Club ($99+/ano). Diferença real para produto bootstrapped.
- Bundle da rota pública não carrega uma linha de WebGL até depois do LCP.

### Ruins e o que fazemos a respeito

- **Duas representações da mesma imagem** — `<img>` no DOM e plano no canvas —
  e elas precisam ficar sincronizadas. É a complexidade central da abordagem.
  Mitigado por isolar tudo num hook (`useSyncedPlane`) e por sincronizar
  apenas imagens visíveis, via `IntersectionObserver`.
- **Layout thrashing** se leitura e escrita se intercalarem. Mitigado por
  agrupar todas as leituras de `getBoundingClientRect` antes de qualquer
  escrita.
- **GLSL é uma linguagem a mais** para manter, sem tipagem e com erro só em
  runtime. Mitigado por shaders em arquivo `.glsl` versionado, escopo pequeno
  (displacement, melt, grão) e teste de degradação em vez de teste de
  aparência.
- **Variante `texture` extra** por foto de portfólio: mais storage e mais um
  passo no worker. Custo pequeho comparado ao original, e obrigatório —
  textura a partir do original seriam 25 MB por foto.
- **`@types/three` fica uma minor atrás do `three`**, e o `three` muda API em
  minor. Consequência prática: fixar as duas na mesma minor. Registrado em
  [16 · Front-end](../16-FRONTEND-E-EXPERIENCIA.md), seção 10.
- **Reconfirmar a licença do GSAP** antes de publicar. Mudança de licença já
  aconteceu duas vezes neste projeto (MediatR, FluentAssertions) e é risco
  registrado em [13 · Roadmap](../13-ROADMAP-E-RISCOS.md).
- **Tema `imersivo` pode ficar ruim** com foto de baixa qualidade, e é
  exatamente o cenário multi-tenant. Mitigado por ser opt-in por tenant e por
  ter Web Vitals por tenant no Sentry para descobrir quem está sofrendo.

## Revisão

Reavaliar o `imersivo` depois de três tenants o usarem em produção, medindo
LCP real e taxa de conversão de lead contra o `editorial`. A hipótese a
derrubar: **efeito aumenta percepção de qualidade sem custar lead.**
