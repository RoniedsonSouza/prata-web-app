# 15 · Estratégia de testes

Não existe QA neste projeto. O teste é o único mecanismo de regressão, e
regressão numa máquina de estados de dinheiro é caro de descobrir em produção.

A régua: **teste é para o que quebra caro.** Máquina de estados, split,
isolamento de tenant e consentimento. Não é para getter.

---

## 1. Pirâmide

| Camada | Projeto | O que prova | Custo | Quantidade |
|---|---|---|---|---|
| Unidade de domínio | `Prata.Domain.Tests` | invariante, transição, cálculo de dinheiro | milissegundos, sem I/O | **a maioria** |
| Caso de uso | `Prata.Application.Tests` | orquestração, autorização, validação, com ports falsos | milissegundos | muitos |
| Integração | `Prata.Api.IntegrationTests` | RLS, isolamento, migration, SQL do Dapper, webhook | segundos, Postgres real | poucos e valiosos |
| Front unitário | `web` + Vitest | hook, formatação, avaliação de `VisibleWhen` | rápido | alguns |
| Front ponta a ponta | Playwright | briefing completo, seleção, link de convidado | lento | 5 a 8 fluxos, no máximo |
| Performance | Lighthouse CI | LCP, INP, CLS, orçamento de bundle | lento | rota pública |

Ferramentas: **xUnit v3**, **AwesomeAssertions** (fork Apache-2.0 do
FluentAssertions v7 — a v8 virou paga), **NSubstitute**, **Bogus**,
**Testcontainers**, **Respawn**. Ver
[`Directory.Packages.props`](../Directory.Packages.props).

## 2. Domínio

### Máquina de estados: teste parametrizado, não trinta testes

Para cada máquina, um teste percorre **todos** os pares estado × transição e
afirma que só os da tabela de [05](05-MAQUINAS-DE-ESTADO.md) passam.

```csharp
[Theory]
[MemberData(nameof(TodosOsParesDeTransicao))]
public void Transicao_fora_da_tabela_falha_e_nao_altera_o_pedido(
    OrderStatus origem, string transicao)
{
    var pedido = PedidoBuilder.Em(origem);

    var resultado = pedido.Executar(transicao);

    if (TabelaDeTransicoes.Permite(origem, transicao))
        resultado.IsSuccess.Should().BeTrue();
    else
    {
        resultado.Error!.Value.Code.Should().Be("PEDIDO_TRANSICAO_INVALIDA");
        pedido.Status.Should().Be(origem);   // não alterou nada
    }
}
```

A segunda asserção é a que pega o bug real: transição inválida que retorna
erro **e mexe no estado** antes de retornar.

### Nome de teste cita a regra

```csharp
public class ConfirmacaoDePedidoTests
{
    [Fact] public void RN_COM_020_nao_confirma_sem_sinal_confirmado() { }
    [Fact] public void RN_COM_020_nao_confirma_sem_contrato_assinado() { }
    [Fact] public void RN_COM_021_confirmacao_e_idempotente_em_qualquer_ordem_de_evento() { }
}
```

O ID no nome é o que liga o teste ao [catálogo de
regras](06-REGRAS-DE-NEGOCIO.md). Quando uma regra muda, `grep RN_COM_020`
acha tudo que precisa mudar com ela.

### Cobertura mínima obrigatória

| Assunto | Regra |
|---|---|
| Toda linha da tabela de transição | um caso positivo |
| Todo par estado × transição fora da tabela | coberto pelo parametrizado |
| Divisão de parcela | 100,00 ÷ 3 = 33,33 + 33,33 + 33,34 ([RN-FIN-004](06-REGRAS-DE-NEGOCIO.md)) |
| Arredondamento de comissão | 8% de 8.000,00; e de 33,33 |
| Faixa do sinal | 29%, 30%, 50%, 51% ([RN-FIN-010](06-REGRAS-DE-NEGOCIO.md)) |
| Política de cancelamento | as três janelas de [RN-COM-040](06-REGRAS-DE-NEGOCIO.md) |
| Snapshot de preço | alterar o pacote não muda pedido existente ([RN-CAT-004](06-REGRAS-DE-NEGOCIO.md)) |
| Snapshot de `SplitRule` | alterar a comissão não muda cobrança existente ([RN-FIN-032](06-REGRAS-DE-NEGOCIO.md)) |
| `VisibleWhen` | dependência circular rejeitada; obrigatória oculta não bloqueia envio ([RN-BRF-010](06-REGRAS-DE-NEGOCIO.md)) |

### Teste de arquitetura

Roda em `Prata.Domain.Tests` e falha o build:

```csharp
[Fact]
public void Domain_nao_referencia_nada_de_fora()
{
    var proibidos = new[]
    {
        "Microsoft.EntityFrameworkCore", "Npgsql", "Dapper", "Hangfire",
        "Microsoft.Extensions", "Serilog", "System.Text.Json", "AWSSDK",
    };
    // assembly de Prata.Domain não referencia nenhum deles
}

[Fact] public void Application_referencia_apenas_Domain() { }
[Fact] public void Nenhum_double_em_caminho_de_dinheiro() { }        // RN-FIN-003
[Fact] public void Toda_entidade_de_negocio_implementa_ITenantOwned() { } // RN-TEN-001
```

## 3. Aplicação

Ports falsos, não mocks de banco. Um `IPaymentGateway` falso que devolve
resultado controlado é rápido, legível e não sabe nada de HTTP.

Cobertura obrigatória:

| Assunto | Exemplo |
|---|---|
| Autorização por papel | `tenant.staff` em endpoint financeiro → `SEM_PERMISSAO` ([RN-TEN-006](06-REGRAS-DE-NEGOCIO.md)) |
| Autorização de dado sensível | `platform.admin` no briefing sensível → negado ([RN-TEN-005](06-REGRAS-DE-NEGOCIO.md)) |
| Idempotência de comando | mesma `Idempotency-Key` duas vezes → uma execução |
| Idempotência de evento | `PagamentoConfirmado` em duplicata → uma confirmação ([RN-COM-021](06-REGRAS-DE-NEGOCIO.md)) |
| Validação | campo obrigatório ausente → `VALIDACAO_FALHOU` com o campo |
| Notificação não desfaz negócio | SMTP falhando → pedido segue confirmado ([RN-NOT-003](06-REGRAS-DE-NEGOCIO.md)) |
| Publicação de evento no outbox | comando bem-sucedido grava `outbox_message` na mesma transação |

### Teste de log

Parece exótico e é obrigatório: [RN-LGP-005](06-REGRAS-DE-NEGOCIO.md) diz que
dado sensível não entra em log, e a única forma de garantir é capturar a
saída.

```csharp
[Fact]
public async Task RN_LGP_005_briefing_sensivel_nao_aparece_em_log()
{
    const string marcador = "MARCADOR-SENSIVEL-UNICO-9f3a";
    var log = new CapturaDeLog();

    await SalvarBriefingComRespostaSensivel(marcador);

    log.Tudo().Should().NotContain(marcador);
}
```

O mesmo padrão vale para token, chave do PSP e string de conexão.

## 4. Integração

**Postgres real, via Testcontainers.** Não é preferência: SQLite in-memory
**não tem RLS**, e RLS é metade da barreira de isolamento. Testar isolamento
em SQLite não testa nada.

| Assunto | O que se verifica |
|---|---|
| **Isolamento de tenant** | seção 5, abaixo |
| Policy de RLS presente | para cada tabela de negócio em `information_schema`, existe policy. Falha em tabela nova sem policy |
| Migration aplica do zero | banco vazio → `database update` → schema esperado |
| Migration é reversível onde deve ser | `down` das migrations não destrutivas |
| Papel do banco | API conectada como owner **se recusa a subir** ([RN-TEN-012](06-REGRAS-DE-NEGOCIO.md)) |
| SQL do Dapper | toda query de listagem executada de verdade; `SELECT` inválido não chega em produção |
| Auditoria append-only | `UPDATE audit_log` falha no banco ([RN-AUD-001](06-REGRAS-DE-NEGOCIO.md)) |
| Sobreposição de reserva | `EXCLUDE` do Postgres impede reserva concorrente (E5) |
| Webhook idempotente | mesmo `external_event_id` 3× → uma transição ([RN-FIN-021](06-REGRAS-DE-NEGOCIO.md)) |
| Webhook fora de ordem | `Liquidado` antes de `Confirmado` não regride ([RN-FIN-023](06-REGRAS-DE-NEGOCIO.md)) |
| Webhook forjado | assinatura inválida → `401` e **nada gravado** ([RN-FIN-020](06-REGRAS-DE-NEGOCIO.md)) |

Isolamento entre testes com **Respawn** (limpa as tabelas entre casos), não
recriando o container — recriar container por teste transforma 40 segundos em
20 minutos.

### Fixtures de webhook

Payload real do PSP salvo como arquivo, não construído em código. Conjunto
mínimo: confirmação, liquidação, reenvio duplicado, chegada fora de ordem,
estorno total, estorno parcial, chargeback, KYC aprovado, KYC reprovado.

**Nunca** teste automatizado batendo na API real do PSP em CI.

## 5. O teste que não pode desaparecer

`Prata.Api.IntegrationTests/TenantIsolationTests.cs`.

```csharp
public class TenantIsolationTests
{
    // Dois tenants com dados equivalentes. Para CADA tabela de negócio,
    // tenta ler e escrever cruzado. TODAS as tentativas precisam falhar.

    [Theory]
    [MemberData(nameof(TodasAsTabelasDeNegocio))]
    public async Task RN_TEN_002_nao_le_linha_do_tenant_vizinho(string tabela) { }

    [Theory]
    [MemberData(nameof(TodasAsTabelasDeNegocio))]
    public async Task RN_TEN_002_nao_escreve_com_tenant_id_do_vizinho(string tabela) { }

    [Fact] public async Task RN_TEN_002_sem_tenant_setado_nao_retorna_nenhuma_linha() { }
    [Fact] public async Task RN_TEN_011_token_do_tenant_A_em_host_do_tenant_B_retorna_403() { }
    [Fact] public async Task RN_TEN_002_query_dapper_sem_tenant_id_e_barrada_pela_RLS() { }
    [Fact] public async Task RN_ENT_004_url_assinada_de_foto_de_outro_tenant_retorna_403() { }
}
```

O terceiro caso é o mais importante e o menos óbvio: **com a variável de
sessão não setada, a policy compara com `NULL` e nenhuma linha passa.** É o
comportamento seguro — esquecer de setar o tenant retorna vazio, nunca tudo.

Roda com o papel `prata_app` (`NOBYPASSRLS`), nunca com o owner. Rodar com o
owner faz o teste passar por engano.

### Gate no CI

Se a string `TenantIsolation` desaparecer de `tests/`, **o build quebra** —
mesmo com todos os outros testes verdes. Ver
[`.github/workflows/ci.yml`](../.github/workflows/ci.yml).

O gate existe porque este é o teste que alguém apaga numa sexta-feira para
destravar um deploy.

## 6. Front

| Camada | Ferramenta | O que cobre |
|---|---|---|
| Unidade | Vitest | avaliação de `VisibleWhen`, formatação de `Money` e data, detecção de capacidade do dispositivo |
| Componente | Vitest + Testing Library | formulário de briefing: condicional, autosave, obrigatoriedade |
| Ponta a ponta | Playwright | 5 a 8 fluxos, no máximo |
| Performance | Lighthouse CI | LCP, INP, CLS, orçamento ([RN-FRT-002](06-REGRAS-DE-NEGOCIO.md)) |
| Bundle | size-limit | 120 kB na rota pública, 200 kB no chunk WebGL ([RN-FRT-001](06-REGRAS-DE-NEGOCIO.md)) |

### Fluxos de ponta a ponta

1. Visitante abre o portfólio, navega uma coleção, inicia um pedido
2. Cliente preenche briefing condicional completo, com autosave e retomada
3. Fotógrafo orça; cliente aprova; sinal é pago (PSP falso); pedido confirma
4. Cliente seleciona favoritas acima do limite e paga o upsell
5. Convidado abre o `ShareLink` com senha e **não consegue** baixar o original
6. Galeria bloqueada por pendência: paga o saldo e a alta resolução destrava

### Testes específicos da camada WebGL

Não se testa "o shader está bonito". Testa-se a **degradação**:

| Caso | Asserção |
|---|---|
| `prefers-reduced-motion: reduce` | canvas não monta; página funcional ([RN-FRT-004](06-REGRAS-DE-NEGOCIO.md)) |
| `save-data: on` | idem |
| `deviceMemory` baixo | idem |
| WebGL2 indisponível | idem |
| `EffectsEnabled = false` no tenant | idem |
| Erro de compilação de shader | degrada em silêncio, sem tela branca ([RN-FRT-007](06-REGRAS-DE-NEGOCIO.md)) |
| JavaScript desligado | toda foto do portfólio presente como `<img>` ([RN-FRT-003](06-REGRAS-DE-NEGOCIO.md)) |
| Portal e back-office | nenhum import de Three/R3F nesses grupos de rota ([RN-FRT-006](06-REGRAS-DE-NEGOCIO.md)) |

O último é um teste de bundle, não de comportamento: se `three` aparecer no
chunk do back-office, falha.

## 7. Cobertura

Percentual global é métrica ruim: 90% com o cálculo de split descoberto é pior
que 60% com ele todo coberto. Alvo por área:

| Área | Alvo | Justificativa |
|---|---|---|
| `Domain/Billing` | **95%** | dinheiro |
| `Domain/Sales` (máquina de estados) | **95%** | núcleo do fluxo |
| `Domain/*` restante | 85% | — |
| `Application/*` | 75% | orquestração |
| `Infrastructure/*` | sem alvo | coberto por integração onde importa |
| `Api/Endpoints` | sem alvo | coberto por integração |

O CI publica cobertura como informação. **Não** quebra o build por
percentual — build que quebra por 0,3% de cobertura ensina a escrever teste
inútil.

## 8. TDD

Para o domínio, teste primeiro — não por doutrina, por economia: escrever
`RN_COM_020_nao_confirma_sem_sinal_confirmado` antes obriga a decidir o que
`Confirmar()` recebe, e essa decisão é mais barata num teste que numa
refatoração depois de três chamadas existirem.

Para adapter de infraestrutura, o valor é menor: escreva o adapter, cubra com
integração.

## 9. O que **não** testar

| Não teste | Por quê |
|---|---|
| Getter, setter, construtor trivial | custo sem retorno |
| Mapeamento de DTO campo a campo | o compilador já garante |
| Que o EF Core funciona | é responsabilidade da Microsoft |
| Framework de terceiro | idem |
| Aparência de shader | não é verificável em asserção; teste a degradação |
| API real do PSP em CI | flaky, lento e sujeito a mudança fora do seu controle |
| Percentual de cobertura como meta | ver seção 7 |
