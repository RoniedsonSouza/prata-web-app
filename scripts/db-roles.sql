-- ===========================================================================
--  Prata - papéis do banco
-- ===========================================================================
--  Uso:
--    psql "postgres://prata_owner:...@localhost:5432/prata" \
--         -v app_password=SUA_SENHA -f scripts/db-roles.sql
--
--  Sem -v app_password, usa a senha de desenvolvimento 'prata_dev_app'.
--
--  Idempotente: pode rodar antes E depois das migrations. Rode de novo
--  depois de cada migration que crie tabela, para reaplicar os grants.
--
--  POR QUE ISSO EXISTE
--  -------------------
--  Superusuário e DONO DA TABELA ignoram Row Level Security. Se a API
--  conectar com o mesmo papel que roda as migrations, a RLS está ligada e
--  não protege nada. Ver docs/03-MULTI-TENANCY.md e RN-TEN-012.
--
--    prata_owner  dono do schema, aplica DDL.  USADO SÓ POR MIGRATION.
--    prata_app    execução da API e do Worker. NOBYPASSRLS, não é dono.
-- ===========================================================================

\set ON_ERROR_STOP on

\if :{?app_password}
\else
  \set app_password 'prata_dev_app'
  \echo '>> app_password não informado: usando a senha de desenvolvimento.'
\endif

-- ---------------------------------------------------------------------------
-- 1. Papel de execução
-- ---------------------------------------------------------------------------
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'prata_app') THEN
    CREATE ROLE prata_app LOGIN NOBYPASSRLS;
    RAISE NOTICE 'papel prata_app criado';
  ELSE
    RAISE NOTICE 'papel prata_app já existe';
  END IF;
END
$$;

-- Reforça NOBYPASSRLS mesmo se o papel já existia com outra configuração
ALTER ROLE prata_app NOBYPASSRLS NOSUPERUSER NOCREATEDB NOCREATEROLE;
ALTER ROLE prata_app PASSWORD :'app_password';

-- ---------------------------------------------------------------------------
-- 2. Permissões
-- ---------------------------------------------------------------------------
GRANT CONNECT ON DATABASE prata TO prata_app;
GRANT USAGE   ON SCHEMA public  TO prata_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES    IN SCHEMA public TO prata_app;
GRANT USAGE, SELECT                  ON ALL SEQUENCES IN SCHEMA public TO prata_app;

-- Tabela criada depois já nasce com o grant certo
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO prata_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO prata_app;

-- prata_app não cria nem altera estrutura: não pode desligar RLS de nada
REVOKE CREATE ON SCHEMA public FROM prata_app;

-- ---------------------------------------------------------------------------
-- 3. Auditoria é append-only (RN-AUD-001)
-- ---------------------------------------------------------------------------
-- Nem a aplicação altera trilha de auditoria. Garantido por permissão no
-- banco, não por convenção de código.
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables
             WHERE table_schema = 'public' AND table_name = 'audit_log') THEN
    REVOKE UPDATE, DELETE ON public.audit_log FROM prata_app;
    RAISE NOTICE 'audit_log: UPDATE e DELETE revogados de prata_app';
  ELSE
    RAISE NOTICE 'audit_log ainda não existe - rode este script de novo depois das migrations';
  END IF;
END
$$;

-- ---------------------------------------------------------------------------
-- 4. Extensão para o EXCLUDE de sobreposição de reserva (E5)
-- ---------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- ---------------------------------------------------------------------------
-- 5. Aviso: owner superusuário anula a barreira 2
-- ---------------------------------------------------------------------------
-- FORCE ROW LEVEL SECURITY alcança o DONO da tabela, mas NÃO alcança
-- superusuário - superusuário ignora RLS incondicionalmente. Em dev a imagem
-- do Postgres cria o POSTGRES_USER como superusuário; em produção o owner
-- não pode ser.
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'prata_owner' AND rolsuper) THEN
    RAISE WARNING 'prata_owner é SUPERUSUÁRIO: ele bypassa a RLS mesmo com FORCE.';
    RAISE WARNING 'Aceitável em desenvolvimento. Em produção, use um owner NOSUPERUSER.';
    RAISE WARNING 'Ver docs/03-MULTI-TENANCY.md, seção 4.';
  END IF;
END
$$;

-- ---------------------------------------------------------------------------
-- 6. Conferência
-- ---------------------------------------------------------------------------
\echo ''
\echo '--- papéis ---'
SELECT rolname,
       rolsuper    AS super,
       rolbypassrls AS bypassa_rls,
       rolcanlogin AS pode_logar
  FROM pg_roles
 WHERE rolname IN ('prata_owner', 'prata_app')
 ORDER BY rolname;

\echo ''
\echo '--- tabelas de negócio sem RLS habilitada (deve vir vazio) ---'
SELECT c.relname AS tabela
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public'
   AND c.relkind = 'r'
   AND NOT c.relrowsecurity
   AND c.relname NOT IN ('tenant', 'platform_user', 'payment_event',
                         '__EFMigrationsHistory')
   AND c.relname NOT LIKE 'hangfire%'
 ORDER BY c.relname;

\echo ''
\echo '>> Se a listagem acima trouxe tabela, falta ENABLE/FORCE ROW LEVEL SECURITY'
\echo '>> na migration que a criou. Ver docs/11-MODELO-DE-DADOS.md, seção 3.'
\echo ''
