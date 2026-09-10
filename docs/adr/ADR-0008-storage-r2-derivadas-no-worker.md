# ADR-0008 · Cloudflare R2 e derivadas no worker

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E4

## Contexto

Um casamento entregue são ~800 fotos: ~20 GB de original mais ~2 GB de
derivadas. E cada galeria compartilhada serve os mesmos gigabytes para
**dezenas** de convidados — o link de convidado é justamente o principal canal
de aquisição orgânica do produto.

Isso faz do **egress**, não do armazenamento, o custo que mata o modelo. Em
provedor tradicional o egress de uma galeria bem compartilhada custa mais que
guardá-la.

## Decisão

- **Storage S3-compatible: Cloudflare R2** em produção. **MinIO** em
  desenvolvimento.
- **Bucket sempre privado.** Nenhum objeto com ACL público. Toda leitura por
  URL assinada com TTL curto (15 min)
  ([RN-ENT-002](../06-REGRAS-DE-NEGOCIO.md)).
- **Upload direto do navegador** para o storage, com URL pré-assinada e
  multipart. O arquivo **não passa pela API**.
- **Derivadas geradas no worker** com ImageSharp, fora do request: `thumb` 480,
  `web` 1600 com marca d'água, `texture` 1024, `lqip` 20 px e cor dominante.
- **O original nunca é alterado nem sobrescrito**
  ([RN-ENT-003](../06-REGRAS-DE-NEGOCIO.md)).
- Prefixo `tenants/{tenantId}/…` — conveniência de custo e operação, **não**
  controle de acesso.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| AWS S3 padrão | egress cobrado por GB. Com o padrão de compartilhamento deste produto, o egress passa o storage |
| Backblaze B2 | egress gratuito até 3× o volume armazenado, o que também funciona. Segunda opção real; R2 ganha pela integração com o CDN da Cloudflare |
| Storage do provedor da aplicação (Fly volumes, disco de VPS) | não escala, não tem CDN, e backup passa a ser problema nosso |
| Derivadas no request, sob demanda | primeiro acesso de cada foto ficaria lento e o request teria que manter 25 MB em memória. Inviável em 800 fotos |
| `imgproxy` em container gerando na borda | boa opção e provavelmente o caminho se o volume crescer muito. Adiciona um serviço para operar agora, sem necessidade |
| Comprimir o original para economizar | **o original é o ativo do fotógrafo.** Não se toca |

## Consequências

### Boas

- **Egress zero.** É a decisão que viabiliza o link de convidado como canal de
  aquisição — cada casamento pode ser compartilhado com 50 pessoas sem custo
  marginal de banda.
- Ordem de grandeza confortável: ~US$ 0,33 por casamento/mês; 100 casamentos
  retidos 12 meses ≈ US$ 33/mês.
- Upload de 20 GB não toca a API: sem timeout, sem banda paga duas vezes, com
  retomada por parte.
- Compatível com S3, então trocar por B2 é mudar `ServiceUrl` e credencial.

### Ruins e o que fazemos a respeito

- **O custo cresce sozinho, todo mês, sem ninguém mexer.** É o único item do
  produto com essa característica. Mitigações, em ordem: **expiração
  contratual de galeria** ([RN-ENT-050](../06-REGRAS-DE-NEGOCIO.md)) e
  **storage frio** após a expiração. Compressão do original é a terceira e a
  pior.
- **O prazo de expiração tem que estar no contrato.** Sem cláusula, a
  expectativa do cliente é "para sempre" — e "para sempre" é uma conta que
  cresce todo mês ([RN-CTR-002](../06-REGRAS-DE-NEGOCIO.md)).
- **URL assinada de 15 min quebra link salvo pelo usuário.** Aceito: é o
  preço do bucket privado. A UI sempre re-assina ao abrir a galeria.
- **ImageSharp tem licença Six Labors Split**, gratuita abaixo de um limite de
  receita anual. Reconferir antes de faturar — registrado em
  [`Directory.Packages.props`](../../Directory.Packages.props). Alternativas
  se o limite for atingido: licença comercial, ou mover a geração para
  `imgproxy`.
- **Console da edição community do MinIO foi reduzido em 2025.** Em
  desenvolvimento, administração de bucket é via `mc` ou AWS CLI. Registrado
  em [`docker-compose.yml`](../../docker-compose.yml).
