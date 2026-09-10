#!/usr/bin/env bash
#
# Prata - consistência da documentação.
# Roda igual na máquina e no CI (.github/workflows/ci.yml).
#
#   ./scripts/check-docs.sh
#
# Verifica:
#   1. link interno quebrado em qualquer .md do repositório
#   2. RN-XXX-000 citada sem estar definida em docs/06-REGRAS-DE-NEGOCIO.md
#   3. ADR referenciada que não existe
#   4. campo de segredo preenchido no .env.example
#
set -uo pipefail
cd "$(dirname "$0")/.."

falhas=0
aviso() { printf '  \033[33m!\033[0m %s\n' "$1"; }
erro()  { printf '  \033[31m✗\033[0m %s\n' "$1"; falhas=$((falhas + 1)); }
ok()    { printf '  \033[32m✓\033[0m %s\n' "$1"; }

# ---------------------------------------------------------------------------
echo
echo "1. Links internos"
# ---------------------------------------------------------------------------
quebrados=0
verificados=0
while IFS= read -r -d '' file; do
  dir="$(dirname "$file")"
  # extrai o destino de todo link markdown [texto](destino),
  # ignorando bloco de codigo cercado e codigo inline entre backticks
  while IFS= read -r target; do
    [ -z "$target" ] && continue
    case "$target" in
      http://*|https://*|mailto:*|"#"*) continue ;;
    esac
    alvo="${target%%#*}"                    # remove âncora
    [ -z "$alvo" ] && continue              # link só de âncora
    verificados=$((verificados + 1))
    if [ ! -e "$dir/$alvo" ]; then
      erro "$file -> $target"
      quebrados=$((quebrados + 1))
    fi
  done < <(awk '
             /^[[:space:]]*```/ { dentro = !dentro; next }
             dentro            { next }
                               { gsub(/`[^`]*`/, ""); print }
           ' "$file" 2>/dev/null \
           | grep -oE '\]\([^)]+\)' \
           | sed -E 's/^\]\(//; s/\)$//')
done < <(find . -name '*.md' -type f -not -path './node_modules/*' -not -path './.git/*' -print0)

[ "$quebrados" -eq 0 ] && ok "$verificados links locais, todos resolvem"

# ---------------------------------------------------------------------------
echo
echo "2. Regras de negócio"
# ---------------------------------------------------------------------------
catalogo=docs/06-REGRAS-DE-NEGOCIO.md
if [ ! -f "$catalogo" ]; then
  erro "catálogo ausente: $catalogo"
else
  # Definidas: linha de tabela que começa com | `RN-XXX-000`
  grep -oE '^\| *`?RN-[A-Z]{3}-[0-9]{3}`? *\|' "$catalogo" \
    | grep -oE 'RN-[A-Z]{3}-[0-9]{3}' | sort -u > /tmp/prata-rn-definidas.txt

  # Citadas: em qualquer lugar do repositório
  grep -rhoE 'RN-[A-Z]{3}-[0-9]{3}' \
      --include='*.md' --include='*.yml' --include='*.yaml' \
      --include='*.props' --include='*.sh' --include='*.sql' \
      --include='*.example' --include='docker-compose.yml' \
      . 2>/dev/null | sort -u > /tmp/prata-rn-citadas.txt
  # docker-compose.yml e .env.example não casam com --include acima
  grep -ohE 'RN-[A-Z]{3}-[0-9]{3}' docker-compose.yml .env.example 2>/dev/null \
    >> /tmp/prata-rn-citadas.txt
  # RN-XXX-000 e o placeholder da convencao de nomenclatura, nao uma citacao
  grep -v '^RN-XXX-000$' /tmp/prata-rn-citadas.txt | sort -u > /tmp/prata-rn-c2.txt
  mv /tmp/prata-rn-c2.txt /tmp/prata-rn-citadas.txt

  definidas=$(wc -l < /tmp/prata-rn-definidas.txt | tr -d ' ')
  citadas=$(wc -l < /tmp/prata-rn-citadas.txt | tr -d ' ')
  orfas=$(comm -13 /tmp/prata-rn-definidas.txt /tmp/prata-rn-citadas.txt)

  if [ -n "$orfas" ]; then
    while IFS= read -r r; do erro "regra citada e não definida: $r"; done <<< "$orfas"
  else
    ok "$definidas regras definidas · $citadas citadas · todas resolvem"
  fi

  # Informativo: regra definida que ninguém cita ainda é perfeitamente válida
  nunca_citadas=$(comm -23 /tmp/prata-rn-definidas.txt /tmp/prata-rn-citadas.txt | wc -l | tr -d ' ')
  [ "$nunca_citadas" -gt 0 ] && aviso "$nunca_citadas regras ainda não citadas fora do catálogo (ok)"
fi

# ---------------------------------------------------------------------------
echo
echo "3. ADRs"
# ---------------------------------------------------------------------------
adr_faltando=0
while IFS= read -r adr; do
  if ! ls docs/adr/"$adr"-*.md >/dev/null 2>&1; then
    erro "ADR referenciada e inexistente: $adr"
    adr_faltando=$((adr_faltando + 1))
  fi
done < <(grep -rhoE 'ADR-[0-9]{4}' --include='*.md' --include='*.yml' --include='*.props' . 2>/dev/null \
         | sort -u)
if [ "$adr_faltando" -eq 0 ]; then
  ok "$(ls -1 docs/adr/ADR-*.md 2>/dev/null | wc -l | tr -d ' ') ADRs, todas as referências resolvem"
fi

# ---------------------------------------------------------------------------
echo
echo "4. Segredos"
# ---------------------------------------------------------------------------
if [ -f .env ] && git ls-files --error-unmatch .env >/dev/null 2>&1; then
  erro ".env está versionado"
fi

if [ ! -f .env.example ]; then
  erro ".env.example ausente"
else
  # Campo cuja CHAVE termina em palavra de segredo, com valor preenchido
  # e sem placeholder. Evita falso positivo em Auth__Password__MinLength=10.
  preenchidos=$(grep -vE 'CHANGE_ME' .env.example \
    | grep -inE '^[A-Za-z0-9_]*(secret|password|token|apikey|api_key|signingkey|accesskey|secretkey)=..*$' \
    || true)
  if [ -n "$preenchidos" ]; then
    while IFS= read -r l; do erro "segredo preenchido no .env.example: ${l%%=*}"; done <<< "$preenchidos"
  else
    ok ".env.example só tem placeholder em campo de segredo"
  fi
fi

# ---------------------------------------------------------------------------
echo
if [ "$falhas" -eq 0 ]; then
  printf '\033[32mDocumentação consistente.\033[0m\n\n'
  exit 0
else
  printf '\033[31m%d problema(s).\033[0m\n\n' "$falhas"
  exit 1
fi
