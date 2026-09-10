# 16 · Front-end e experiência

O portfólio público precisa de layout e movimento em nível de estúdio
premiado. Este documento diz **como** fazer isso sem quebrar as duas coisas
que sustentam o produto: SEO e custo.

---

## 1. Correção da spec: Next.js 16

A spec original dizia Next.js 15. A linha atual é a **16** (`16.3.4` em
set/2026); o 15 está em manutenção por backport (`15.5.x`).

Começar um produto de manutenção longa uma versão maior atrás é dívida de
graça — a mesma lógica que levou o backend de .NET 9 para .NET 10 LTS.
**Decisão: Next.js 16.**

## 2. A tensão, e como se resolve

Dois fatos do escopo entram em rota de colisão com o "modo Awwwards" padrão:

1. O portfólio é **onde o SEO do fotógrafo acontece** ([00](00-VISAO-E-ESCOPO.md)).
2. **Egress e storage são o risco de custo nº 1** ([13](13-ROADMAP-E-RISCOS.md)).

E há um terceiro, específico deste produto: um site de agência premiada é uma
peça sob medida, com curadoria manual de conteúdo. Aqui é um **template que N
fotógrafos usam com fotos que você não escolheu.** Shader de distorção em foto
mal enquadrada não fica premiado — fica estranho.

**A conclusão não é menos efeito.** É:

> Não se constrói um site Awwwards. Constrói-se um **motor de temas** de nível
> Awwwards.

Isso é melhor para o produto: vira diferencial vendável por tenant em vez de
custo fixo de manutenção.

## 3. As três superfícies

| Grupo de rota | Superfície | Render | Motion | WebGL |
|---|---|---|---|---|
| `(public)` | portfólio, coleções, serviços | SSG + ISR | GSAP + Lenis | **por tema** |
| `(portal)` | portal do cliente | SSR autenticado | Motion, transição de rota | **nunca** |
| `(studio)` | back-office | CSR + TanStack Query | micro-interação apenas | **nunca** |

[RN-FRT-006](06-REGRAS-DE-NEGOCIO.md) é verificada por teste de bundle: se
`three` aparecer no chunk de `(portal)` ou `(studio)`, o build falha.
Back-office que anima é back-office que irrita.

## 4. As três camadas

### Camada 1 · Base — sempre presente, E1

O que existe mesmo sem JavaScript.

| Item | Decisão |
|---|---|
| Render | RSC + ISR, revalidação por tag no publish |
| Imagem | `next/image` com AVIF/WebP, `sizes` correto, `priority` só no LCP |
| Placeholder | `lqip` base64 de 20 px + cor dominante, **gerados pelo worker ImageSharp** no mesmo passe das derivadas |
| CLS | zero, por construção: `width`/`height` sempre presentes ([RN-FRT-002](06-REGRAS-DE-NEGOCIO.md)) |
| CSS | Tailwind v4, `@theme` CSS-first, tokens de tenant em custom properties |
| Tipografia | variable font self-hosted, `next/font/local`, `clamp()` para escala fluida, `text-wrap: balance` em título |
| HTML | semântico. Toda foto é `<img>` real, com `alt` obrigatório |

O `lqip` é o melhor custo-benefício de todo o front: sai grátis no worker que
já processa a imagem, e resolve CLS e percepção de velocidade de uma vez.

### Camada 2 · Motion — E1

| Ferramenta | Uso | Nota |
|---|---|---|
| **GSAP + ScrollTrigger** | coreografia de scroll, pin, timeline, `SplitText` em título | Padrão da indústria. **Gratuito, todos os plugins inclusos**, depois da aquisição pela Webflow — antes `SplitText` e `ScrollSmoother` eram Club ($99+/ano). Reconfirmar a licença antes de publicar |
| **Lenis** (~3 kb) | smooth scroll com inércia | Integra com ScrollTrigger via `lenis.on('scroll', ScrollTrigger.update)`. **Desativado em touch e sob `prefers-reduced-motion`** ([RN-FRT-005](06-REGRAS-DE-NEGOCIO.md)) |
| **Motion** (`motion/react`) | transição de UI, animação de layout, gesto | Ex-Framer Motion. Usado no portal e no studio, **não** na coreografia do portfólio |
| **View Transitions API** | morph foto → lightbox | Progressivo: sem suporte, degrada para fade |

**Por que Lenis e não ScrollSmoother:** o Locomotive v5 hoje é essencialmente
um wrapper do Lenis, e o ScrollSmoother (agora gratuito) amarra a camada de
scroll ao GSAP. O Lenis é mais leve e agnóstico. Se você preferir stack única
de GSAP, trocar é meia hora — está isolado num provider.

**Por que Lenis desligado no touch:** a inércia nativa do iOS é melhor que
qualquer emulação em JS, e `syncTouch` é fonte conhecida de travamento. Isso
não é limitação: é a decisão certa.

### Camada 3 · WebGL — E4, só no tema premium

| Ferramenta | Uso |
|---|---|
| **R3F + Three.js** | cena declarativa, composição em componente, `Suspense` para textura |
| **GLSL** | shader de displacement, distorção, transição "melt", grão, partícula |
| `@react-three/drei` | `useTexture`, `<Image>`, helpers de câmera |
| `@react-three/postprocessing` | bloom, grão de filme, aberração cromática — **com parcimônia** |
| **OGL** (~10 kb) | escape hatch: se o chunk do R3F estourar o orçamento, um shader de imagem não precisa de scene graph |

**PixiJS: recusado.** Sobrepõe 100% com o Three, seria um segundo renderer e
um segundo contexto WebGL no mesmo bundle. Pixi ganha em milhares de sprites;
um portfólio tem dezenas de fotos. Campo de partícula, se aparecer, sai de
`<Points>` com instancing no próprio R3F. Ver
[ADR-0010](adr/ADR-0010-motion-e-webgl.md).

## 5. A técnica central: DOM primeiro, canvas sobreposto

É isto que faz o efeito coexistir com SEO. Sem isso, nada aqui funciona.

```
┌─────────────────────────────────────────────────────────────────┐
│  DOM  (fonte da verdade: layout, SEO, acessibilidade, LCP)      │
│                                                                 │
│   <figure>                                                      │
│     <img src="…web-1600.webp" width height alt="…" />           │
│   </figure>                                                     │
│         ▲                                                       │
│         │  getBoundingClientRect()  a cada frame relevante      │
│         │                                                       │
│  ┌──────┴──────────────────────────────────────────────────┐    │
│  │  <canvas>  position: fixed; inset: 0; pointer-events:   │    │
│  │            none; z-index acima do conteúdo              │    │
│  │                                                          │    │
│  │  plano WebGL na MESMA posição e escala da <img>,         │    │
│  │  texturizado com …texture-1024.webp                      │    │
│  └──────────────────────────────────────────────────────────┘    │
│                                                                 │
│  Quando a textura carrega e o shader compila:                   │
│    img.style.opacity = 0    ← só então o DOM some               │
└─────────────────────────────────────────────────────────────────┘
```

Sequência exata:

| # | Passo |
|---|---|
| 1 | HTML chega do servidor com todas as `<img>`. Google indexa, LCP conta, funciona sem JS |
| 2 | Hidratação. Detecção de capacidade (seção 8). Reprovou? **Fim** — a página já está correta |
| 3 | Após o LCP pintar, `requestIdleCallback` dispara o import dinâmico do chunk WebGL |
| 4 | Canvas monta transparente, sobreposto, `pointer-events: none` |
| 5 | Textura `texture-1024` carrega; shader compila |
| 6 | Só agora as `<img>` correspondentes vão para `opacity: 0` — com transição, sem salto |
| 7 | Falha em qualquer passo → canvas é desmontado, `<img>` volta a `opacity: 1` |

O que isso entrega:

- **Google vê HTML e imagem.** Nenhum conteúdo indexável dentro do canvas
  ([RN-FRT-003](06-REGRAS-DE-NEGOCIO.md)).
- **LCP é a `<img>` do DOM**, não o canvas. O WebGL nunca entra no caminho da
  métrica.
- **O usuário vê** displacement, distorção no hover e transição derretendo.
- **Falha degrada em silêncio** ([RN-FRT-007](06-REGRAS-DE-NEGOCIO.md)).
- O leitor de tela lê a `<img>` — que continua no DOM, só invisível.

O caminho oposto — construir a galeria dentro do canvas — entrega o mesmo
efeito visual e custa SEO, acessibilidade, LCP e a possibilidade de degradar.
**Nunca faça isso aqui.**

### Sincronização

```ts
// Esboço do contrato. O plano segue a <img>, não o contrário.
function sincronizarPlano(img: HTMLImageElement, mesh: Mesh, camera: Camera) {
  const r = img.getBoundingClientRect()          // ler
  mesh.scale.set(r.width, r.height, 1)           // escrever
  mesh.position.set(
    r.left + r.width  / 2 - innerWidth  / 2,
    -(r.top + r.height / 2) + innerHeight / 2,
    0,
  )
}
```

Detalhe de performance que decide 60 fps: **agrupe todas as leituras de
`getBoundingClientRect` e só depois escreva** nos meshes. Intercalar leitura e
escrita força layout thrashing no navegador. Um `IntersectionObserver`
mantém a lista de imagens visíveis, e só essas são sincronizadas.

## 6. Motor de temas

Três temas curados. **Não** é construtor livre de site: três temas bem
resolvidos batem qualquer drag-and-drop em resultado visual, e é escopo
negativo declarado em [00](00-VISAO-E-ESCOPO.md).

| Tema | Visual | Motion | WebGL | Para quem | Etapa |
|---|---|---|---|---|---|
| `editorial` | tipografia grande, grid assimétrico, muito respiro | GSAP reveal + parallax leve | **não** | padrão de todos | **E1** |
| `cinema` | full-bleed, scroll horizontal por coleção, cortes secos | GSAP pin + timeline | grão + transição | casamento | E4 |
| `imersivo` | capa com shader, displacement no hover, "melt" na troca | GSAP + R3F | **sim** | premium | E4 |

O tenant escolhe **tema + paleta + par tipográfico**. Nada além disso —
liberdade demais produz portfólio feio, e portfólio feio não vende ensaio.

### Tokens por tenant

Tema resolvido em **runtime**, não em build. Um tenant novo não gera deploy.

```css
/* Tailwind v4: @theme define os tokens; o tenant sobrescreve as variáveis */
@theme {
  --color-ink:      oklch(0.15 0.02 260);
  --color-paper:    oklch(0.98 0.005 90);
  --color-accent:   oklch(0.62 0.18 25);
  --font-display:   "Fraunces", Georgia, serif;
  --font-body:      "Inter Tight", system-ui, sans-serif;
  --radius-frame:   2px;
  --motion-scale:   1;      /* 0 sob prefers-reduced-motion */
}
```

Servido por `GET /v1/t/{slug}/theme` ([10 · API](10-API.md)) e injetado no
`<head>` do server component. `oklch` porque interpolação de gradiente e
estado de hover em cor perceptualmente uniforme não produz o cinza morto que
`hsl` produz.

### Par tipográfico curado

Fonte arbitrária por tenant destrói performance **e** bom gosto. Set fechado,
todas variáveis e self-hosted:

| Par | Display | Corpo | Caráter |
|---|---|---|---|
| `serifado` | Fraunces | Inter Tight | editorial, casamento clássico |
| `alto-contraste` | Instrument Serif | Geist | moderno, revista |
| `expressivo` | Bricolage Grotesque | Satoshi | autoral, ensaio criativo |
| `neutro` | Geist | Geist | corporativo, book profissional |

Regras: `font-display: swap`, subset latino, `preload` só do peso do LCP,
sempre com pilha de fallback real — nunca `sans-serif` solto.

## 7. Orçamento de performance

Deixa de ser aspiração e vira **build quebrado**.

| Métrica | Limite | Ferramenta |
|---|---|---|
| LCP p75 mobile | ≤ 2,0 s | Lighthouse CI |
| INP | ≤ 200 ms | Lighthouse CI |
| CLS | ≤ 0,05 | Lighthouse CI |
| JS inicial da rota pública | **≤ 120 kB gzip, sem WebGL** | size-limit |
| Chunk WebGL | **≤ 200 kB gzip**, carregado após o LCP | size-limit |
| CSS | ≤ 40 kB | budget do Lighthouse |
| Fontes | ≤ 120 kB, no máximo 4 arquivos | budget |
| Imagem acima da dobra | ≤ 900 kB total | budget |
| Nota de SEO | **100** | Lighthouse CI |
| Nota de acessibilidade | ≥ 95 | Lighthouse CI |

Configuração em [`.github/lighthouse/`](../.github/lighthouse/lighthouserc.json)
e [`budget.json`](../.github/lighthouse/budget.json).
[RN-FRT-001](06-REGRAS-DE-NEGOCIO.md), [RN-FRT-002](06-REGRAS-DE-NEGOCIO.md).

Complemento em produção: **Web Vitals por tenant no Sentry**. É o que responde
o que o CI não responde — qual tema afunda qual portfólio, com as fotos reais
daquele fotógrafo. Ver [14 · Ambientes e operação](14-AMBIENTES-E-OPERACAO.md).

## 8. Matriz de capacidade e fallback

O WebGL só monta se **todas** passarem ([RN-FRT-004](06-REGRAS-DE-NEGOCIO.md)):

| Condição | Verificação | Se falhar |
|---|---|---|
| Movimento aceito | `matchMedia('(prefers-reduced-motion: no-preference)')` | sem WebGL, sem Lenis, `--motion-scale: 0` |
| Sem economia de dados | `navigator.connection?.saveData !== true` | sem WebGL |
| Memória suficiente | `navigator.deviceMemory >= 4` | sem WebGL |
| CPU suficiente | `navigator.hardwareConcurrency >= 4` | sem WebGL |
| WebGL2 disponível | criação de contexto bem-sucedida | sem WebGL |
| Ligado no tenant | `effectsEnabled` de `TenantSettings` | sem WebGL |
| Ligado globalmente | `NEXT_PUBLIC_EFFECTS_ENABLED` | sem WebGL |
| Não é touch | para o **Lenis** apenas | inércia nativa do sistema |

`deviceMemory` e `saveData` não existem no Safari — ausência é tratada como
**aprovação**, não reprovação. Bloquear todo iPhone por API ausente seria pior
que o problema.

O último token é o interruptor de operação: `NEXT_PUBLIC_EFFECTS_ENABLED=false`
derruba o WebGL de todos os tenants sem redeploy de tema. É o que se usa às
23h de sábado quando um driver novo do Chrome começa a travar.

## 9. Acessibilidade

Não é opcional, e o gate de 95 no Lighthouse não cobre tudo:

| Item | Regra |
|---|---|
| `prefers-reduced-motion` | derruba WebGL, Lenis e toda animação de entrada. Não "reduz": desliga |
| Navegação por teclado | toda coleção e toda foto alcançável por `Tab`, com foco visível |
| Foco | o canvas tem `pointer-events: none` e `aria-hidden="true"` — nunca captura foco |
| Leitor de tela | lê a `<img>` do DOM, que continua lá |
| Smooth scroll | não pode quebrar `Ctrl+F`, âncora nem `scroll-to-anchor` do navegador |
| Contraste | AA mínimo em todos os pares de tema. Verificado no CI |
| `alt` | obrigatório em `collection_item` no banco ([11](11-MODELO-DE-DADOS.md)) — não é campo opcional |
| Movimento parasita | nada que pisque, gire em loop infinito ou se mova sem interação |

A linha do `alt` obrigatório no banco é deliberada: acessibilidade que depende
da boa vontade de quem cadastra não acontece.

## 10. Dependências

Versões verificadas no npm em **2026-09-10**. Confirmar com `npm outdated`
antes do primeiro commit de `web/`.

### Base — E1

| Pacote | Versão | Nota |
|---|---|---|
| `next` | 16.3.4 | ver seção 1 |
| `react` · `react-dom` | 19.3.0 | — |
| `typescript` | 7.0.2 | compilador nativo, muito mais rápido. Se algum plugin de lint não acompanhar, o fallback é a linha 5.x |
| `tailwindcss` · `@tailwindcss/postcss` | 4.3.3 | `@theme` CSS-first |
| `clsx` · `tailwind-merge` | 2.1.1 · 3.6.0 | — |
| `lucide-react` | 1.44.0 | ícones |
| `zod` | 4.6.1 | validação compartilhada com o formulário |
| `next-themes` | 0.4.6 | claro/escuro |
| `@sentry/nextjs` | 10.74.0 | erro + Web Vitals por tenant |

`shadcn` (CLI 4.21.0) gera componente **para dentro** do repositório; não é
dependência de runtime.

### Motion — E1

| Pacote | Versão | Nota |
|---|---|---|
| `gsap` | 3.15.0 | ScrollTrigger, SplitText, Flip inclusos e gratuitos |
| `lenis` | 1.3.26 | smooth scroll |
| `motion` | 13.2.0 | ex-`framer-motion`; import de `motion/react` |

`split-type` (0.3.4) **não** é necessário: o `SplitText` do GSAP virou
gratuito e faz o mesmo, melhor.

### Portal e studio — E2

| Pacote | Versão |
|---|---|
| `@tanstack/react-query` | 5.102.8 |
| `react-hook-form` · `@hookform/resolvers` | 7.87.0 · 5.9.1 |
| `date-fns` | 4.4.0 |
| `embla-carousel-react` | 8.6.0 |

### WebGL — E4

| Pacote | Versão | Nota |
|---|---|---|
| `three` | 0.186.0 | **fixar em `0.185.x` se quiser tipos casados** — ver aviso abaixo |
| `@types/three` | 0.185.4 | atrasado uma minor em relação ao `three` |
| `@react-three/fiber` | 9.7.0 | — |
| `@react-three/drei` | 10.7.8 | — |
| `@react-three/postprocessing` | 3.1.1 | + `postprocessing` 6.39.5 |
| `maath` | 0.10.8 | utilitários de matemática |
| `ogl` | 1.0.11 | escape hatch, se o chunk estourar |

> **Aviso prático.** `@types/three` costuma ficar uma minor atrás do `three`.
> Como o `three` muda API em minor, a combinação `three@0.186` +
> `@types/three@0.185` produz erro de tipo em API nova. Fixe as duas na mesma
> minor (`0.185.x`) até os tipos alcançarem, ou aceite o desalinhamento
> conscientemente.

### Testes e gates

| Pacote | Versão |
|---|---|
| `vitest` | 5.0.0 |
| `@playwright/test` | 1.63.0 |
| `size-limit` | 13.0.3 |
| `@lhci/cli` | 0.15.1 |

### Organização dos shaders

```
web/src/gl/
├── shaders/
│   ├── displacement.vert.glsl
│   ├── displacement.frag.glsl
│   ├── melt.frag.glsl
│   ├── grain.frag.glsl
│   └── lib/simplex3d.glsl        ← incluído por concatenação, não por import mágico
├── glsl.d.ts                     ← declare module '*.glsl' { const s: string; export default s }
├── materials/
└── hooks/useSyncedPlane.ts
```

`.glsl` em arquivo, não em template literal: ganha realce de sintaxe, `diff`
legível e `.gitattributes` já trata como texto.

## 11. SEO

| Item | Implementação |
|---|---|
| Render | SSG + ISR. Toda página pública tem HTML completo na primeira resposta |
| Metadata | API de metadata do Next por tenant e por coleção ([RN-VIT-006](06-REGRAS-DE-NEGOCIO.md)) |
| Canônica | por tenant. Na E5, com domínio próprio, a canônica passa a ser o domínio dele |
| `sitemap.xml` · `robots.txt` | gerados por tenant, só com coleção publicada ([RN-VIT-007](06-REGRAS-DE-NEGOCIO.md)) |
| Dados estruturados | JSON-LD: `LocalBusiness` + `Photograph` + `ImageObject` por coleção |
| OG image | gerada com `@vercel/og` a partir da capa da coleção |
| Imagem | `alt` real vindo do banco, `sizes` correto, AVIF/WebP |
| Nunca | conteúdo indexável dentro de canvas ([RN-FRT-003](06-REGRAS-DE-NEGOCIO.md)) |

## 12. Anti-padrões

O que **não** fazer, com o motivo.

| Anti-padrão | Por quê |
|---|---|
| Galeria construída **dentro** do canvas | mata SEO, acessibilidade, LCP e a possibilidade de degradar |
| Preloader com contador de porcentagem | é LCP artificialmente adiado. O conteúdo já está no HTML — mostre-o |
| Scroll hijack sem `prefers-reduced-motion` | causa mal-estar real em quem tem sensibilidade a movimento |
| Lenis com `syncTouch` no mobile | fonte conhecida de travamento. Inércia nativa é melhor |
| Fonte arbitrária escolhida pelo tenant | destrói performance e resultado visual |
| Textura WebGL a partir do original | 25 MB de textura por foto. Use `texture-1024` |
| PixiJS junto do Three | dois renderers, dois contextos, bundle dobrado, zero ganho |
| `unsafe-inline` na CSP para viabilizar animação | abre XSS por conveniência de shader |
| Bloquear clique direito | teatro. Ver [09](09-GALERIAS-E-ENTREGA.md), seção 9 |
| Animação de entrada no back-office | irrita quem usa o sistema oito horas por dia |
| `useEffect` sincronizando canvas a cada scroll sem `requestAnimationFrame` | garante queda de frame |
| Ler `getBoundingClientRect` e escrever no mesh de forma intercalada | layout thrashing. Leia tudo, depois escreva tudo |
