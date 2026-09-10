# E4 · Entrega

**Marco:** fecha o ciclo — e liga o canal de indicação.
**Ordem de grandeza:** 4 a 6 semanas, meio período.
**Depende de:** E1, E2, E3.

> **Esqueleto de propósito.** Ver a nota em [E3](E3-DINHEIRO.md). A referência
> técnica completa está em
> [09 · Galerias e entrega](../09-GALERIAS-E-ENTREGA.md) e
> [16 · Front-end](../16-FRONTEND-E-EXPERIENCIA.md).

---

## 1. Objetivo

Entregar as fotos, cobrar o que falta usando a própria galeria como alavanca, e
transformar cada casamento em dezenas de visitantes no portfólio do fotógrafo.

## 2. Escopo

| Entra | Não entra |
|---|---|
| Upload direto multipart por URL assinada | agenda e contrato (E5) |
| Worker de derivadas: thumb, web+marca, texture, lqip | app móvel |
| `Gallery`, `Photo`, `PhotoVariant` | edição de imagem na plataforma |
| Máquina de estados da galeria | álbum impresso |
| `BloqueadaPorPendencia` | watermark invisível / DRM |
| Seleção com limite e upsell | |
| `ShareLink` para convidado | |
| ZIP em background | |
| Expiração, avisos e storage frio | |
| **Temas `cinema` e `imersivo`** (camada WebGL) | |

A camada WebGL entra aqui, e não na E1, por dois motivos: ela depende da
variante `texture` que o worker desta etapa gera, e é a única parte do front
que pode ser cortada sem afetar o funcionamento do produto.

## 3. Decisões a fechar no início da etapa

| Decisão | Nota |
|---|---|
| Preço do excedente de seleção: por foto ou em bloco | afeta a UI do upsell, não o modelo |
| Storage frio: R2 Infrequent Access ou Backblaze B2 | decidir com o **custo real medido** na E3/E4, não com tabela de preço |
| Convidado pode baixar a derivada `web` com marca d'água? | configurável por tenant, mas o padrão precisa ser decidido |

## 4. Critério de aceite

- [ ] Galeria de casamento real entregue: ~800 fotos, upload direto, derivadas
      completas
- [ ] Upload de 20 GB retomando após queda de conexão
- [ ] `BloqueadaPorPendencia` funcionando: saldo pago **destrava** a alta
      resolução
- [ ] Original barrado antes de `SelecaoFechada` + saldo
- [ ] Seleção acima do limite exigindo upsell confirmado
- [ ] ZIP de 20 GB montado em background sem timeout
- [ ] Link de convidado acessado por **mais de 15 pessoas distintas**
- [ ] Convidado **não** consegue baixar original
- [ ] Job de expiração emitindo os avisos de 30/7/1 dia
- [ ] Custo de storage do mês conferido contra a estimativa de
      [09](../09-GALERIAS-E-ENTREGA.md), seção 8
- [ ] Tema `imersivo` dentro do orçamento: chunk WebGL ≤ 200 kB, LCP ≤ 2,0 s
- [ ] Degradação verificada: sem WebGL2, com `prefers-reduced-motion`, com
      `save-data` e sem JavaScript, a página continua correta

## 5. Riscos da etapa

| Risco | Mitigação |
|---|---|
| Custo de storage saindo do previsto | expiração contratual + storage frio; medir todo mês |
| Upload de 20 GB falhando em 4G | multipart com retomada por parte, testado em rede ruim de verdade |
| Galeria travada em `EmPreparo` por derivada com falha | alarme em taxa de falha > 10% e reenfileiramento |
| Cliente achando `BloqueadaPorPendencia` constrangedor | **perguntar ao piloto antes de implementar.** Se ele não usar, a alavanca não existe |
| Tema `imersivo` estourando o orçamento | orçamento como gate de CI; OGL como escape hatch |
| `imersivo` ficando ruim com foto de baixa qualidade | opt-in por tenant + Web Vitals por tenant no Sentry |
| ZIP duplicado por duplo clique | idempotência por escopo de download |
| Original apagado por acidente | nenhum caminho de código apaga original sem ação explícita e auditada do owner |

## 6. Referências

[09 · Galerias e entrega](../09-GALERIAS-E-ENTREGA.md) ·
[16 · Front-end](../16-FRONTEND-E-EXPERIENCIA.md) ·
[06 · Regras](../06-REGRAS-DE-NEGOCIO.md), prefixos `ENT` e `FRT` ·
[ADR-0008](../adr/ADR-0008-storage-r2-derivadas-no-worker.md) ·
[ADR-0010](../adr/ADR-0010-motion-e-webgl.md)
