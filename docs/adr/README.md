# Decisões de arquitetura (ADR)

Uma decisão por arquivo. Cada uma tem contexto, alternativas consideradas,
decisão e consequências — inclusive as ruins.

**Regra:** decisão revogada **não se apaga**. Muda-se o status para
`Substituída por ADR-00NN` e mantém-se o texto. O registro de por que se
tentou o caminho errado vale mais que a limpeza do diretório.

| # | Decisão | Status | Etapa |
|---|---|---|---|
| [0001](ADR-0001-dotnet-10-clean-architecture.md) | .NET 10 LTS, Clean Architecture, dispatcher próprio | aceita | E1 |
| [0002](ADR-0002-banco-unico-tenantid-rls.md) | Banco único com `tenant_id` + RLS obrigatória | aceita | E1 |
| [0003](ADR-0003-modelo-de-receita-comissao.md) | Receita por comissão por transação na v1 | aceita | E3 |
| [0004](ADR-0004-subdominio-na-v1.md) | Só subdomínio na v1; domínio próprio na E5 | aceita | E1 |
| [0005](ADR-0005-psp-asaas-com-port.md) | Asaas atrás do port `IPaymentGateway` | aceita | E3 |
| [0006](ADR-0006-assinatura-eletronica-propria.md) | Aceite próprio com trilha de auditoria | aceita | E5 |
| [0007](ADR-0007-whatsapp-wame-na-v1.md) | `wa.me` na v1; Cloud API na E5 | aceita | E2 |
| [0008](ADR-0008-storage-r2-derivadas-no-worker.md) | Cloudflare R2 + derivadas no worker | aceita | E4 |
| [0009](ADR-0009-idioma-do-codigo.md) | Inglês no código, enums de estado em português | aceita | E1 |
| [0010](ADR-0010-motion-e-webgl.md) | GSAP + Lenis + R3F; PixiJS recusado | aceita | E1/E4 |

## Formato

```markdown
# ADR-00NN · Título

**Status:** proposta | aceita | substituída por ADR-00MM
**Data:** AAAA-MM-DD
**Etapa:** E1..E5

## Contexto
## Decisão
## Alternativas consideradas
## Consequências
### Boas
### Ruins e o que fazemos a respeito
```

Escreva ADR quando a decisão **fecha uma alternativa** — não para registrar
escolha óbvia. "Usar HTTPS" não é ADR.
