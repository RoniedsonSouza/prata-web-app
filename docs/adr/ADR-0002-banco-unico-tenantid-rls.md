# ADR-0002 · Banco único com `tenant_id` e RLS obrigatória

**Status:** aceita
**Data:** 2026-09-10
**Etapa:** E1

## Contexto

Multi-tenant com N fotógrafos. Cada tenant tem foto de casamento de cliente
dele e resposta de briefing sobre o próprio corpo. **Vazamento entre tenants é
o único risco classificado como crítico do produto** — não é bug, é fim do
negócio.

Isso é decisão de fundação: mudar depois custa uma migração inteira e uma
janela de indisponibilidade.

## Decisão

- **Banco único, schema único, `tenant_id` em toda tabela de negócio.**
- **Três barreiras**, todas obrigatórias:
  1. `HasQueryFilter` global no EF Core;
  2. **Row Level Security** no PostgreSQL, com `USING`, `WITH CHECK` e
     `FORCE`;
  3. `TenantId` explícito em toda assinatura de repositório e toda query
     Dapper.
- **Dois papéis de banco**: `prata_owner` (dono, só migration) e `prata_app`
  (`NOBYPASSRLS`, execução). A API valida no boot e **se recusa a subir** se
  estiver conectada como owner.
- **EF Core para escrita**, Dapper para listagem — e query Dapper sempre com
  `tenant_id` parametrizado.
- Chave única de usuário é `(tenant_id, email)`, nunca `email`.

Detalhe completo em [03 · Multi-tenancy](../03-MULTI-TENANCY.md).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Um schema por tenant | migration × N tenants; conexão pool fragmentado; onboarding deixa de ser instantâneo |
| Um banco por tenant | isolamento máximo e custo operacional máximo. Faz sentido com tenant enterprise pagando por isso — não é o caso |
| Só query filter do EF, sem RLS | é a decisão que causa o vazamento. Job de background, Dapper e `IgnoreQueryFilters()` correm fora do filtro |
| Só RLS, sem query filter | funcionaria, mas cada consulta dependeria de a variável de sessão estar setada, sem rede de segurança na aplicação |
| Soft delete com filtro global | um filtro esquecido a mais para vazar. Exclusão real + `AuditLog` |

## Consequências

### Boas

- Onboarding de tenant é um `INSERT`, não uma migration.
- Barato e simples até alguns milhares de tenants.
- Bug de aplicação **não** vaza dado: a RLS pega.
- Acesso direto ao banco por credencial de aplicação também não vaza.

### Ruins e o que fazemos a respeito

- **Duas strings de conexão** para manter e não confundir. Mitigado pela
  recusa de boot ([RN-TEN-012](../06-REGRAS-DE-NEGOCIO.md)) — o erro é
  impossível de passar em silêncio.
- **RLS adiciona sobrecarga** em toda query. Medida e aceitável; o índice de
  toda tabela começa por `tenant_id`.
- **Toda migration nova precisa lembrar** de `ENABLE`/`FORCE` + policy.
  Mitigado por teste de integração que varre `information_schema` e falha em
  tabela de negócio sem policy.
- **Um tenant gigante degrada os vizinhos** (noisy neighbor). Aceito nesta
  escala; a saída futura é mover esse tenant para banco próprio, e o modelo
  com `tenant_id` em tudo permite isso.
- **Teste de isolamento exige Postgres real.** Testcontainers no CI, mais
  lento. Aceito: SQLite não tem RLS, e testar isolamento em SQLite não testa
  nada.
