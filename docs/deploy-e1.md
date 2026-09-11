# Deploy E1 (docs/etapas/E1-FUNDACAO.md §3.7)
#
# Front: Vercel com wildcard `*.prata.app` apontando para este projeto `web/`.
#   - Root Directory: web
#   - Env: NEXT_PUBLIC_API_URL, REVALIDATE_SECRET, SENTRY_DSN, NEXT_PUBLIC_SENTRY_DSN
#
# API: container a partir do Dockerfile na raiz do monorepo.
#   docker build -t prata-api .
#   docker run --env-file .env -p 8080:8080 prata-api
#
# Banco: Postgres gerenciado; migrations com ConnectionStrings__PrataOwner;
# runtime da API apenas com ConnectionStrings__PrataApp (RN-TEN-012).
#
# Seq: defina Seq__ServerUrl (e opcional Seq__ApiKey) no cofre do provedor.
# Sem URL, a API so escreve no console.
