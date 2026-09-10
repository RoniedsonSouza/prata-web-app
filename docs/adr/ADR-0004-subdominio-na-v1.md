# ADR-0004 · Só subdomínio na v1; domínio próprio na E5

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E1 (subdomínio) · E5 (domínio próprio)

## Contexto

O portfólio é o canal de aquisição do fotógrafo, e SEO é o que faz ele
funcionar. Domínio próprio (`joaosilva.com.br`) é melhor para marca e para
SEO de longo prazo que subdomínio (`joao-silva.prata.app`).

Mas domínio próprio na fundação adiciona: verificação de propriedade do
domínio, emissão e renovação automática de certificado, orientação de DNS para
um público não técnico, e uma classe nova de ticket de suporte — "meu site
está fora" quando o CNAME foi apagado.

## Decisão

**E1: só subdomínio.** `{slug}.prata.app`, com certificado wildcard.
Resolução por middleware antes da autenticação.

**E5: domínio próprio** via CNAME, com emissão automática de certificado. A
coluna `tenant.custom_domain` já existe no modelo desde a E1, sem uso — para
não exigir migração depois.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Domínio próprio já na E1 | melhor SEO e marca, mas adiciona verificação de domínio, ciclo de certificado e suporte de DNS na etapa em que ainda não há um único tenant pagante |
| Só path (`prata.app/joao-silva`), sem subdomínio | mais simples de infra, mas divide autoridade de SEO entre todos os tenants no mesmo domínio, e não permite migrar para domínio próprio sem quebrar toda URL indexada |
| Subdomínio + `rel=canonical` apontando para o domínio próprio desde o começo | complexidade de duas identidades sem nenhuma delas pronta |

## Consequências

### Boas

- Infra de E1 trivial: um certificado wildcard resolve todos os tenants.
- Onboarding instantâneo: o fotógrafo escolhe o slug e o site está no ar.
- Subdomínio indexa normalmente — o SEO da E1 não fica bloqueado.
- Zero ticket de DNS na fase em que cada hora conta.

### Ruins e o que fazemos a respeito

- **Autoridade de domínio compartilhada** entre tenants em `prata.app`. Menos
  bom que domínio próprio, e é o motivo de a E5 existir.
- **Slug é imutável após publicar** ([RN-TEN-003](../06-REGRAS-DE-NEGOCIO.md)),
  porque mudá-lo quebra link indexado e link de galeria já enviado por
  WhatsApp. Consequência: a tela de escolha de slug precisa avisar isso de
  forma explícita, antes de confirmar.
- **Migração para domínio próprio na E5 exige `301`** de todas as URLs do
  subdomínio, e preservar o subdomínio funcionando por tempo indeterminado.
  Registrado agora para não ser surpresa.
- **Slug reservado é armadilha silenciosa.** Lista validada no cadastro, com
  `s` e `t` incluídos por causa das rotas `/s/{token}` e `/t/{slug}`.
