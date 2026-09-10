# ADR-0001 · .NET 10 LTS, Clean Architecture e dispatcher próprio

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E1

## Contexto

O núcleo do produto é estado de pedido, split de pagamento e conciliação
financeira. É domínio de regra densa, com muita transição válida e inválida, e
com custo alto de erro. O desenvolvedor entrega em C# várias vezes mais rápido
que em qualquer outra linguagem.

A spec inicial mencionava .NET 9. O .NET 9 é STS e saiu de suporte em maio de
2026. O **.NET 10 é LTS até novembro de 2028**, traz C# 14 e EF Core 10. O
.NET 11 (STS) chega em novembro de 2026 e não interessa a um produto de
manutenção longa.

Sobre o dispatcher: o padrão de mercado para Clean Architecture em .NET era
MediatR. **A partir da v13 o MediatR exige licença comercial paga.** Para um
SaaS comercial isso é custo recorrente por uma abstração de ~40 linhas.

## Decisão

- **.NET 10 LTS**, C# 14, ASP.NET Core Minimal APIs.
- **Clean Architecture** em quatro projetos: `Domain` → `Application` →
  `Infrastructure` / `Api`. `Domain` não referencia nada.
- **Dispatcher próprio** sobre o container de DI, com pipeline de behaviors
  registrado manualmente. Sem MediatR.
- **AutoMapper também fora**: mapeamento explícito. Mapeamento mágico esconde
  bug de campo esquecido exatamente em DTO de dinheiro.
- Monólito modular. **Sem microsserviço.**

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Tudo em TypeScript (Node + Next full-stack) | Defensável e, para muitos devs solo, seria o conselho. Aqui perde: a velocidade de entrega deste desenvolvedor em C# é muito maior, e o domínio de dinheiro é onde tipagem forte e teste de unidade barato pagam o próprio custo |
| .NET 9 | fora de suporte desde maio/2026 |
| .NET 11 STS | ciclo de suporte curto para produto de anos |
| MediatR pago | ~40 linhas de dispatcher não justificam licença recorrente |
| Wolverine | boa biblioteca, mas traz modelo de mensageria que não precisamos agora |
| Arquitetura em camadas simples (sem `Domain` isolado) | máquina de estados de dinheiro sem domínio isolado vira `if` espalhado em controller |
| Microsserviços | um dev meio período. Seria dívida operacional, não arquitetura |
| Event sourcing | auditoria resolve com `AuditLog` + outbox, a um décimo da complexidade |

## Consequências

### Boas

- Domínio testável em milissegundos, sem banco.
- Trocar de PSP custa uma classe em `Infrastructure/Payments/`.
- Suporte garantido até nov/2028, sem migração forçada no meio do roadmap.
- Zero custo de licença nas dependências do caminho crítico.

### Ruins e o que fazemos a respeito

- **Duas linguagens** (C# e TypeScript): dois ecossistemas, duas cadeias de
  build. Aceito — não há alternativa razoável para SEO de portfólio fora do
  Next.
- **Mais cerimônia** que um CRUD direto: mais arquivos por caso de uso.
  Mitigado por manter `Application` fino, sem camada de serviço redundante.
- **Regra de dependência se viola por acidente.** Mitigado por teste de
  arquitetura que falha o build — ver
  [15 · Estratégia de testes](../15-ESTRATEGIA-DE-TESTES.md).
- **Dispatcher próprio é código nosso para manter.** São ~40 linhas e não
  mudam. Se um dia precisarmos de mensageria real, Wolverine entra por trás
  da mesma interface.
