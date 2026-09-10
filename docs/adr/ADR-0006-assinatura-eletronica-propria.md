# ADR-0006 · Aceite próprio com trilha de auditoria

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E5

## Contexto

O contrato precisa ser assinado antes de o pedido ir para `Confirmado`, e a
assinatura precisa sustentar uma disputa de chargeback: a prova de que o
cliente aceitou aquele documento, naquela data.

A Lei 14.063/2020 reconhece três níveis de assinatura eletrônica. A
**assinatura eletrônica simples** (art. 4º) é válida para negócio entre as
partes; ICP-Brasil só é exigida em interação com o poder público e em atos
específicos. Contrato de prestação de serviço fotográfico não está nesse
grupo.

## Decisão

**Aceite próprio**, com trilha de auditoria completa:

| Elemento | Guardado em |
|---|---|
| Hash SHA-256 do PDF exato que foi exibido | `contract.pdf_sha256` |
| IP de origem | `signature.ip` |
| User-agent | `signature.user_agent` |
| Timestamp com fuso | `signature.signed_at` |
| Nome e e-mail do signatário | `signature.signer_*` |
| Registro imutável da operação | `audit_log` |

E **port `ISignatureProvider`** em `Application/Abstractions/`, para que
trocar por provedor externo custe uma classe.

Contrato `Assinado` é **imutável**: correção gera contrato novo com referência
ao anterior ([RN-CTR-011](../06-REGRAS-DE-NEGOCIO.md)).

## Alternativas consideradas

| Alternativa | Por que não agora |
|---|---|
| **ZapSign** desde a E5 | mais barato que Clicksign, com carimbo de tempo e validação de identidade. Custo por documento e uma integração a manter numa etapa já cheia. É o candidato natural se a assinatura própria for questionada |
| **Clicksign** | mais reconhecido no mercado jurídico brasileiro. Custo por documento maior; faz sentido se o ticket médio for alto |
| Assinatura com certificado ICP-Brasil | não é exigida neste tipo de contrato, e exigiria certificado do cliente final — atrito que mataria a conversão |
| Aceite simples sem hash do PDF | é o erro comum. Sem o hash, não se prova **qual** documento foi aceito, e a assinatura não sustenta disputa |

## Consequências

### Boas

- Custo zero por documento.
- Zero dependência externa no caminho crítico da confirmação do pedido.
- O hash do PDF é a parte que realmente importa em disputa, e ela é nossa.
- Port pronto: trocar por ZapSign não toca no domínio.

### Ruins e o que fazemos a respeito

- **Não há terceiro atestando a assinatura.** Em disputa, a prova é a nossa
  trilha. Mitigação: hash + IP + user-agent + timestamp + `audit_log`
  imutável, que é um conjunto forte; e o port pronto para trocar se um caso
  real mostrar que não basta.
- **Sem carimbo de tempo de terceiro**, a data depende do nosso relógio.
  Aceitável para contrato entre as partes. Se virar problema, o provedor
  externo resolve.
- **O PDF tem que ser preservado exatamente** como exibido — regerar o PDF com
  fonte diferente muda o hash e invalida a prova. Consequência prática: o PDF
  assinado é armazenado, não regerado sob demanda.
- **QuestPDF tem licença Community com limite de receita anual.** Reconferir
  antes de faturar — registrado em
  [`Directory.Packages.props`](../../Directory.Packages.props).
- **Não é parecer jurídico.** O contrato modelo e o termo de uso precisam de
  revisão de advogado antes de ir ao ar.
