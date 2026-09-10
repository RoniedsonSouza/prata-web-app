# ADR-0009 · Inglês no código, enums de estado em português

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E1

## Contexto

Produto brasileiro, domínio brasileiro, usuário brasileiro. Documentação em
pt-BR. Mas C#, os frameworks e toda a literatura de Clean Architecture são em
inglês.

Há um caso específico que decide a questão: **estado de pedido é a palavra que
o fotógrafo usa ao telefone.** Quando ele liga dizendo "o pedido da Maria está
confirmado", a distância entre `Confirmado` e `Confirmed` é atrito de tradução
mental em todo suporte, todo log e toda conversa.

## Decisão

| Elemento | Idioma | Exemplo |
|---|---|---|
| Tipo, método, propriedade, variável | **inglês** | `Order`, `Payment`, `PayoutAccount`, `CreateOrderCommand` |
| **Membro de enum de estado de negócio** | **português** | `OrderStatus.Rascunho`, `PaymentStatus.Liquidado`, `GalleryStatus.BloqueadaPorPendencia` |
| Método de transição de estado | **português** | `order.Confirmar()`, `gallery.BloquearPorPendencia()` |
| Código de erro de domínio | **português**, `SCREAMING_SNAKE` | `PEDIDO_TRANSICAO_INVALIDA`, `REPASSE_BLOQUEADO_KYC` |
| Tabela e coluna de banco | **inglês**, `snake_case` | `customer_order`, `payout_account`, `tenant_id` |
| Nome de teste | **português**, com o ID da regra | `RN_COM_020_nao_confirma_sem_sinal_confirmado` |
| Comentário e documentação | **português** | — |
| Mensagem visível ao usuário | **português** | — |
| Commit | **português** | — |

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Tudo em inglês, inclusive estados | consistente e é o padrão da indústria. Mas cria tradução mental em todo suporte e em todo log: `Confirmed` no log, "confirmado" no telefone. E o glossário viraria dicionário obrigatório |
| Tudo em português, inclusive tipos | `PedidoRepositorio`, `IProvedorDePagamento`. Fica estranho contra as APIs do framework (`IServiceCollection`, `DbContext`) e afasta contribuidor futuro |
| Estados em inglês e tela em português, com camada de tradução | uma camada a manter para nada. Rótulo em i18n é justificável em produto multi-idioma; aqui é BRL, pt-BR, escopo negativo declarado |

## Consequências

### Boas

- **A linguagem ubíqua é a que o fotógrafo fala.** Log, código, tela e
  conversa dizem `Confirmado`.
- Tipos e frameworks continuam idiomáticos em C#.
- Nome de teste com o ID da regra liga teste, catálogo e código por `grep`.

### Ruins e o que fazemos a respeito

- **Mistura de idiomas no mesmo arquivo:** `public Result<Unit> Confirmar()`
  dentro de `class Order`. É estranho na primeira leitura. Mitigado por ser
  uma regra clara e estreita — só estado de negócio e transição, nunca
  infraestrutura.
- **Risco de deriva:** alguém escreve `OrderStatus.Confirmed` por hábito.
  Mitigado pelo [01 · Glossário](../01-GLOSSARIO.md), que é a fonte, e por
  revisão de PR.
- **Acento em identificador.** C# aceita, mas **não usamos**:
  `BloqueadaPorPendencia`, nunca `BloqueadaPorPendência`. Acento em
  identificador quebra `grep`, cria dois nomes visualmente idênticos e sofre
  com normalização Unicode.
- **Contribuidor estrangeiro** teria atrito. Não é cenário previsto; se
  passar a ser, o glossário é a tradução.
