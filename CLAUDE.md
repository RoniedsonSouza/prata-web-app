# Instruções de construção — Prata

Regras que valem em **todo** commit deste repositório, para pessoa ou agente.
São curtas de propósito: o detalhe está em `docs/`, e este arquivo aponta.

> **Contexto do repositório hoje:** só documentação. `src/`, `tests/` e `web/`
> ainda não existem — são criados na [E1](docs/etapas/E1-FUNDACAO.md).
> Não crie página, componente ou entidade sem que a etapa correspondente tenha
> começado.

---

## 1. Antes de escrever qualquer código

1. Leia a etapa atual em `docs/etapas/`. Ela lista o que entra **e o que não
   entra**.
2. Procure a regra em [`docs/06-REGRAS-DE-NEGOCIO.md`](docs/06-REGRAS-DE-NEGOCIO.md).
   Se o comportamento não tem `RN`, **crie a regra primeiro** — com ID novo,
   nunca reaproveitado.
3. Se a decisão fecha uma alternativa de arquitetura, escreva um ADR antes.
4. Confira o [glossário](docs/01-GLOSSARIO.md). Use o termo de lá, não um
   sinônimo.

## 2. As seis regras que não se negociam

| # | Regra | Onde |
|---|---|---|
| 1 | **`Domain` não referencia nada.** `Application` só referencia `Domain`. `Infrastructure` e `Api` apontam para dentro | [02](docs/02-ARQUITETURA.md) |
| 2 | **Toda tabela de negócio tem `tenant_id NOT NULL`**, implementa `ITenantOwned`, e a migration inclui `ENABLE` + `FORCE ROW LEVEL SECURITY` + policy | [03](docs/03-MULTI-TENANCY.md) · `RN-TEN-001` |
| 3 | **Transição de estado vive na entidade**, com nome de negócio e retornando `Result`. Nunca `entity.Status = X` num handler | [05](docs/05-MAQUINAS-DE-ESTADO.md) |
| 4 | **Dinheiro é `Money`** no domínio e `numeric(14,2)` no banco. `double` e `float` são proibidos em qualquer caminho de dinheiro | `RN-FIN-003` |
| 5 | **Nenhum dado de cartão** é armazenado, logado ou trafegado. Só token do PSP | `RN-FIN-002` |
| 6 | **Dado sensível de briefing não entra em log, trace, métrica, erro nem e-mail** | `RN-BRF-031` · `RN-LGP-005` |

## 3. Multi-tenancy — checklist de todo PR que toca persistência

Vazamento entre tenants é o **único risco crítico** do produto.

- [ ] Tabela nova: `tenant_id uuid NOT NULL`, sem default, `ITenantOwned`
- [ ] Tabela nova: `ENABLE` + `FORCE RLS` + policy na migration
- [ ] Query Dapper nova recebe `TenantId` como **parâmetro explícito**
- [ ] Nenhum `IgnoreQueryFilters()` novo fora de `Persistence/Admin/`
- [ ] Todo `ExecuteUpdate`/`ExecuteDelete` tem `Where` com `TenantId`
- [ ] Chave de cache nova tem prefixo `prata:{tenantId}:`
- [ ] Job novo recebe `TenantId` no payload
- [ ] Índice novo **começa** por `tenant_id`
- [ ] Existe teste que tenta ler o tenant vizinho **e falha**

Detalhe e as dez armadilhas conhecidas: [03](docs/03-MULTI-TENANCY.md), seção 7.

## 4. Testes

- **TDD no domínio.** Escreva o teste da regra antes: `RN_COM_020_nao_confirma_sem_sinal_confirmado`.
  O ID no nome liga teste, catálogo e código por `grep`.
- **Máquina de estados: teste parametrizado**, percorrendo todos os pares
  estado × transição. Nunca trinta testes escritos à mão.
- **Integração com Postgres real** (Testcontainers). SQLite **não tem RLS** —
  testar isolamento em SQLite não testa nada.
- **`TenantIsolationTests` não pode desaparecer.** O CI quebra se a string
  sumir de `tests/`, mesmo com tudo verde.
- O teste de isolamento roda com o papel `prata_app`, **nunca** com o owner —
  rodar com o owner faz o teste passar por engano.

Estratégia completa, alvos de cobertura e o que **não** testar:
[15](docs/15-ESTRATEGIA-DE-TESTES.md).

## 5. Convenções de código

| Elemento | Idioma / forma |
|---|---|
| Tipo, método, propriedade, variável | inglês — `Order`, `PayoutAccount` |
| **Membro de enum de estado de negócio** | **português** — `OrderStatus.Rascunho` |
| Método de transição | português — `order.Confirmar()` |
| Código de erro de domínio | português, `SCREAMING_SNAKE` — `PEDIDO_TRANSICAO_INVALIDA` |
| Tabela e coluna | inglês, `snake_case` — `payout_account` |
| Nome de teste | português, com o ID da regra |
| Comentário, doc, commit, mensagem ao usuário | português |
| Acento em identificador | **nunca** — `BloqueadaPorPendencia`, não `Pendência` |

Motivo em [ADR-0009](docs/adr/ADR-0009-idioma-do-codigo.md). Estilo e
severidade de analisador em [`.editorconfig`](.editorconfig) — o build trata
aviso como erro.

## 6. Dependências

- Versão se declara **só** em [`Directory.Packages.props`](Directory.Packages.props).
  No `.csproj` vai `<PackageReference Include="..." />` sem `Version`.
- **Confira a licença antes de adicionar pacote.** Três casos já mordidos
  neste projeto:

| Pacote | Situação | O que usamos |
|---|---|---|
| MediatR | pago desde a v13 | dispatcher próprio, ~40 linhas |
| FluentAssertions | pago desde a v8 | `AwesomeAssertions` (fork Apache-2.0 da v7) |
| ImageSharp · QuestPDF · GSAP | limite de receita / licença mudou recentemente | reconferir **antes de faturar** |

## 7. Antes de abrir PR

```bash
./scripts/check-docs.sh                     # sempre
dotnet csharpier check .                    # quando houver código
dotnet build                                # avisos são erros
dotnet test
cd web && npm run typecheck && npm run size # orçamento de bundle
```

- Regra de negócio nova ou alterada → atualize
  [`06`](docs/06-REGRAS-DE-NEGOCIO.md) no **mesmo** PR.
- Campo de dado pessoal novo → entrada no inventário de
  [`12`, seção 4](docs/12-SEGURANCA-E-LGPD.md), senão o PR reprova.
- Nunca renumere uma `RN`. Regra revogada fica, com a nota
  `Revogada por RN-XXX-000`.
- ADR revogada **não se apaga**: muda o status para `Substituída por ADR-00NN`.

## 8. Front-end

- **DOM primeiro, canvas sobreposto.** Toda foto do portfólio é `<img>` real,
  indexável. O WebGL monta depois do LCP e degrada em silêncio.
  Nunca construa a galeria dentro do canvas.
- **Portal e back-office: zero WebGL, zero scroll hijack.** Se `three`
  aparecer nesses chunks, o build falha.
- **Orçamento é gate de CI**, não aspiração: 120 kB na rota pública,
  200 kB no chunk WebGL, LCP ≤ 2,0 s, CLS ≤ 0,05, SEO 100.
- `prefers-reduced-motion` **desliga** movimento — não reduz.

Detalhe, matriz de fallback e anti-padrões:
[16](docs/16-FRONTEND-E-EXPERIENCIA.md).

## 9. Segredos

- `.env` está no `.gitignore` e continua lá. Só `.env.example`, sempre com
  `CHANGE_ME`.
- Segredo que apareceu em log ou commit é **comprometido**: rotacione
  primeiro, investigue depois. Apagar o commit não desfaz a exposição.
- Nunca ecoe token, senha, chave do PSP ou string de conexão em saída de
  terminal, log ou mensagem.

## 10. Commits

Português, imperativo, com escopo do módulo:

```
feat(sales): confirma pedido com sinal e contrato (RN-COM-020)
fix(billing): idempotencia de webhook por external_event_id (RN-FIN-021)
docs(adr): registra recusa do PixiJS (ADR-0010)
test(tenancy): isolamento cruzado em todas as tabelas (RN-TEN-002)
```

Cite a `RN` ou o `ADR` quando o commit implementa um dos dois. É o que liga
código, regra e decisão.
