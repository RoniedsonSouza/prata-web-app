-- Prata · policies de RLS aplicadas apos as migrations EF.
-- Padrao: ENABLE + FORCE + policy com USING e WITH CHECK (docs/03).
-- Roda com prata_owner. Idempotente.

DO $$
DECLARE
  r record;
BEGIN
  FOR r IN
    SELECT c.relname AS table_name
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    JOIN information_schema.columns col
      ON col.table_schema = n.nspname AND col.table_name = c.relname
    WHERE n.nspname = 'public'
      AND c.relkind = 'r'
      AND col.column_name = 'tenant_id'
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', r.table_name);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', r.table_name);
    EXECUTE format('DROP POLICY IF EXISTS %I ON %I', r.table_name || '_tenant_isolation', r.table_name);
    EXECUTE format(
      'CREATE POLICY %I ON %I
         USING (tenant_id = nullif(current_setting(''prata.tenant_id'', true), '''')::uuid)
         WITH CHECK (tenant_id = nullif(current_setting(''prata.tenant_id'', true), '''')::uuid)',
      r.table_name || '_tenant_isolation',
      r.table_name
    );
  END LOOP;
END $$;
