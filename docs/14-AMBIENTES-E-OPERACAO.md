# 14 · Ambientes e operação

Regra que orienta tudo aqui: **nada que exija plantão.** Um desenvolvedor meio
período não sustenta infraestrutura própria. Serviço gerenciado sempre que o
custo permitir.

---

## 1. Ambientes

| Ambiente | Onde | Banco | Storage | PSP |
|---|---|---|---|---|
| **Local** | máquina + `docker compose` | Postgres 17 em container | MinIO | sandbox do Asaas |
| **Preview** | Vercel (front) + container efêmero (API) | branch do Neon | bucket `-preview` | sandbox |
| **Produção** | Vercel (front) + Fly.io ou Azure Container Apps (API/Worker) | Neon ou RDS | Cloudflare R2 | produção |

Não existe "homologação" separada: preview por PR cobre o caso e não vira um
ambiente esquecido com dado velho. Migration destrutiva é a exceção — essa se
ensaia num restore do backup de produção, não em preview.

## 2. Subir o ambiente local

```bash
# 1. Dependências
docker compose up -d --wait api-deps

# 2. Papéis do banco (uma vez)
#    O SQL está em docs/11-MODELO-DE-DADOS.md, seção "Papéis do banco".
#    Sem isso a API se recusa a subir (RN-TEN-012).
psql "postgres://prata_owner:prata_dev_only@localhost:5432/prata" -f scripts/db-roles.sql

# 3. Configuração
cp .env.example .env      # e preencher os CHANGE_ME

# 4. Ferramentas .NET
dotnet tool restore

# 5. Migrations (roda como prata_owner)
dotnet ef database update --project src/Prata.Infrastructure --startup-project src/Prata.Api

# 6. API + Worker
dotnet watch --project src/Prata.Api
dotnet watch --project src/Prata.Worker

# 7. Front
cd web && npm install && npm run dev
```

| Serviço local | Endereço |
|---|---|
| API | http://localhost:5080 · OpenAPI em `/scalar` |
| Front | http://prata.localhost:3000 |
| Tenant de teste | http://estudio-demo.prata.localhost:3000 |
| Postgres | `localhost:5432` |
| Redis | `localhost:6379` |
| MinIO (S3) | `localhost:9000` |
| Mailpit (caixa de e-mail) | http://localhost:8025 |
| Seq (log + trace) | http://localhost:5341 |

### Subdomínio em `localhost`

`*.localhost` resolve para `127.0.0.1` sem configuração no Chrome, Firefox e
na maioria dos sistemas. Se o seu não resolver, adicione ao `/etc/hosts`:

```
127.0.0.1  prata.localhost estudio-demo.prata.localhost
```

Webhook do PSP em desenvolvimento precisa de túnel:

```bash
cloudflared tunnel --url http://localhost:5080
# aponte a URL gerada + /v1/webhooks/asaas no painel do Asaas sandbox
# header asaas-access-token = Payments__Asaas__WebhookSecret
```

## 3. Deploy

| Componente | Alvo | Nota |
|---|---|---|
| Front | Vercel | ISR e `next/image` funcionam sem configuração. Wildcard `*.prata.app` no projeto — ver [deploy-e1.md](deploy-e1.md) |
| API | Fly.io ou Azure Container Apps | contêiner, escala a zero fora de horário na fase inicial |
| Worker | mesmo contêiner da API, processo separado | Hangfire com dashboard restrito a `platform.admin` |
| Banco | Neon (branch por PR) ou RDS | Neon simplifica o preview; RDS ganha em previsibilidade de custo em escala |
| Storage | Cloudflare R2 + CDN | bucket privado, acesso só por URL assinada |
| Redis | Upstash ou Redis gerenciado | `maxmemory-policy noeviction` obrigatório |
| PSP | Asaas | `Payments__Provider=Asaas` + chaves; Fake se `CHANGE_ME`. Webhook `/v1/webhooks/asaas` |

### Ordem de deploy

```
1. Migration (compatível com a versão anterior do código)
2. API + Worker
3. Front
```

Migration antes, e **sempre compatível com o código anterior**. Coluna nova
entra `NULL`able, é populada, e só depois vira `NOT NULL` em outra migration.
Isso permite rollback do código sem rollback do banco — que é o rollback que
não existe.

## 4. Jobs

| Job | Agenda | O que faz | Alarme se |
|---|---|---|---|
| `ProcessarOutbox` | a cada 10 s | entrega evento de domínio | fila > 100 ou item com > 5 min |
| `GerarDerivadas` | por evento | thumb, web+marca, texture, lqip | falha após 3 tentativas |
| `MontarZip` | sob demanda | ZIP em streaming para o export | job > 30 min |
| `ExpirarOrcamentos` | diário 03:00 | `OrcamentoEnviado` → `Expirado` | não rodou |
| `CobrarSaldo` | diário 08:00 | gera cobrança do saldo no prazo | não rodou |
| `LembrarAssinatura` | diário 09:00 | lembretes em 1, 3 e 7 dias ([RN-CTR-020](06-REGRAS-DE-NEGOCIO.md)) | não rodou |
| `BloquearPorPendencia` | diário 04:00 | `Disponivel` → `BloqueadaPorPendencia` | não rodou |
| `FecharSelecaoVencida` | diário 04:30 | fecha seleção no prazo | não rodou |
| `AvisarExpiracaoGaleria` | diário 07:00 | e-mail em 30/7/1 dia | não rodou |
| `ExpirarGalerias` | diário 05:00 | `Expirada` → storage frio | não rodou |
| `LimparExports` | horário | apaga ZIP vencido | — |
| `ReconciliarPsp` | diário 05:00 | compara com o extrato do PSP | **qualquer divergência** ([RN-FIN-050](06-REGRAS-DE-NEGOCIO.md)) |
| `ReprocessarRepasseKyc` | diário 06:00 | destrava `Payout` em `BloqueadoKyc` | — |
| `ExpurgarBriefingSensivel` | diário 02:00 | apaga bloco sensível 12 meses após a entrega ([RN-LGP-004](06-REGRAS-DE-NEGOCIO.md)) | **não rodou por 2 dias** |
| `PurgarChavesIdempotencia` | diário 01:00 | limpa chaves > 24 h | — |

Dois alarmes merecem destaque: `ReconciliarPsp` porque divergência de dinheiro
não espera, e `ExpurgarBriefingSensivel` porque job de retenção que não roda é
descumprimento de política de privacidade acumulando silenciosamente.

## 5. Observabilidade

| Camada | Ferramenta | O que se olha |
|---|---|---|
| Log estruturado | Serilog → Seq (local) / Grafana Loki (produção) | erro, requisição lenta, falha de job |
| Trace | OpenTelemetry → OTLP | latência ponta a ponta, N+1 do EF, chamada ao PSP |
| Métrica | OpenTelemetry | taxa de erro, duração de job, tamanho da fila do outbox |
| Front | Sentry | erro de JS, **Web Vitals por tenant** |
| Banco | log de query > 300 ms | N+1 e índice faltando |

### Contexto obrigatório em todo log

`correlation_id` · `tenant_id` · `user_id` · `route` · `duration_ms`

E o que **nunca** aparece: token, senha, chave do PSP, string de conexão, dado
de cartão, resposta sensível de briefing
([RN-LGP-005](06-REGRAS-DE-NEGOCIO.md)). Filtro explícito no Serilog, não
confiança na disciplina de quem escreve o log.

### Web Vitals por tenant

O Sentry no front reporta LCP/INP/CLS **com o slug do tenant**. Isso responde
a pergunta que o Lighthouse do CI não responde: *qual tema está afundando qual
portfólio, com as fotos reais daquele fotógrafo.* Um tenant com 40 fotos de
8 MB no tema `imersivo` tem um problema que nenhum teste sintético mostra.

## 6. Alarmes

Só o que justifica interromper alguém. Alarme que não gera ação vira ruído e
depois vira alarme ignorado.

| Severidade | Condição | Ação |
|---|---|---|
| **Crítico** | webhook do PSP falhando > 5 min | verificar assinatura e disponibilidade; eventos ficam na fila do PSP |
| **Crítico** | divergência de conciliação aberta | investigar antes de qualquer repasse manual |
| **Crítico** | API se recusando a subir por papel de banco errado | corrigir a string de conexão. **Nunca** "resolver" apontando para o owner |
| **Crítico** | taxa de erro 5xx > 2% por 5 min | — |
| Alto | fila do outbox > 100 itens | worker parado ou evento em loop de falha |
| Alto | job de expurgo sensível sem rodar por 2 dias | risco de política de privacidade |
| Alto | derivadas falhando > 10% | galeria não sai de `EmPreparo` e o cliente não vê nada |
| Médio | LCP p75 > 2,5 s em qualquer tenant | investigar tema e peso das imagens |
| Médio | storage crescendo acima da projeção | conferir política de expiração |
| Baixo | certificado a vencer em < 14 dias | renovação automática deveria ter agido |

## 7. Runbooks

Esqueleto. Cada um vira documento próprio quando o incidente acontecer pela
primeira vez — runbook escrito antes do primeiro incidente é ficção.

| Situação | Primeiros passos |
|---|---|
| Webhook do PSP com falha acumulada | conferir assinatura e segredo · consultar situação na API do PSP · reprocessar `payment_event` não processado (idempotente por construção) |
| Divergência de conciliação | comparar `payment` com o extrato · **não** dar baixa automática · registrar a resolução com ator ([RN-FIN-051](06-REGRAS-DE-NEGOCIO.md)) |
| Galeria travada em `EmPreparo` | ver derivadas com falha · reenfileirar `GerarDerivadas` · conferir cota do storage |
| Suspeita de vazamento entre tenants | **preservar log e `audit_log` primeiro** · rodar `TenantIsolationTests` contra produção replicada · seguir [12](12-SEGURANCA-E-LGPD.md), seção 9 |
| Segredo exposto em log ou commit | **rotacionar primeiro**, investigar depois |
| Repasse parado | conferir `kyc_status` · conferir `Payout` em `BloqueadoKyc` · confirmar que o fotógrafo vê o aviso no back-office |
| Custo de storage acima do previsto | conferir job de expiração · conferir ZIP não limpo · conferir derivada duplicada |

## 8. Backup e restauração

| Item | Política |
|---|---|
| Banco | backup automático do provedor + PITR de 7 dias |
| **Teste de restauração** | trimestral, em ambiente separado. Backup nunca testado não é backup |
| Storage | versionamento no bucket + `lifecycle` para storage frio |
| Original de foto | é o ativo mais insubstituível do produto. Nunca há caminho de código que apague original sem ação explícita e auditada do `tenant.owner` |
| Segredos | no cofre do provedor, com cópia de recuperação fora dele |
| Retenção de backup | 30 dias |

A linha do original merece ênfase: dinheiro errado se corrige com estorno;
foto de casamento apagada não volta.
