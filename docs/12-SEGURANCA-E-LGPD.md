# 12 · Segurança e LGPD

Dois assuntos no mesmo documento porque, neste produto, eles são o mesmo
problema: a base guarda **foto de casamento, resposta sobre o próprio corpo e
dado de recebimento**. Cada um desses vaza de um jeito diferente.

> Não é parecer jurídico. Política de privacidade, termo de uso e contrato
> modelo precisam de revisão de advogado antes de ir ao ar.

---

## 1. Matriz de permissão

`✓` permitido · `—` negado (`403`) · `◐` parcial, com a restrição na nota.

| Recurso / operação | `platform.admin` | `tenant.owner` | `tenant.staff` | `client` | convidado | `anon` |
|---|---|---|---|---|---|---|
| Portfólio público (ler) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Tenant: criar, suspender | ✓ | — | — | — | — | — |
| `TenantSettings`: tema, prazos | ◐ ¹ | ✓ | — | — | — | — |
| Equipe e convites | — | ✓ | — | — | — | — |
| Catálogo: serviços e pacotes | — | ✓ | ◐ ² | — | — | — |
| Coleções do portfólio | — | ✓ | ✓ | — | — | — |
| Pedido (ler, do tenant) | ◐ ³ | ✓ | ✓ | ◐ ⁴ | — | — |
| Pedido: orçar | — | ✓ | — | — | — | — |
| Pedido: analisar, recusar, pôr em espera | — | ✓ | ✓ | — | — | — |
| Briefing — respostas **não** sensíveis | ◐ ³ | ✓ | ✓ | ◐ ⁴ | — | — |
| Briefing — respostas **sensíveis** | **—** | ✓ | ◐ ⁵ | ◐ ⁴ | — | — |
| Ficha de direção (PDF) | — | ✓ | ◐ ⁵ | — | — | — |
| Financeiro: a receber, liquidado, repassado | ◐ ³ | ✓ | **—** | — | — | — |
| `PayoutAccount` e KYC | ◐ ³ | ✓ | **—** | — | — | — |
| `SplitRule` (comissão) | ✓ | ◐ ⁶ | — | — | — | — |
| Conciliação: consultar | ✓ | ✓ | — | — | — | — |
| Conciliação: dar baixa | ✓ | — | — | — | — | — |
| Galeria: criar, subir foto, publicar | — | ✓ | ✓ | — | — | — |
| Galeria: ver derivadas | — | ✓ | ✓ | ✓ | ◐ ⁷ | — |
| Galeria: baixar **original** | **—** | ✓ | ✓ | ◐ ⁸ | **—** | — |
| Seleção de favoritas | — | ✓ | ✓ | ✓ | ◐ ⁷ | — |
| Contrato: enviar | — | ✓ | — | — | — | — |
| Contrato: assinar | — | — | — | ✓ | — | — |
| `AuditLog`: consultar | ✓ | ◐ ⁹ | — | — | — | — |
| Exportar / excluir dado do titular | — | ✓ | — | ✓ | — | — |

1. Só leitura, para suporte. Alteração de tema é do dono do estúdio.
2. Leitura. Criar e precificar é do dono.
3. Só metadado e agregado: contagem, status, valor total. Sem conteúdo de
   briefing e sem URL de foto — [RN-TEN-005](06-REGRAS-DE-NEGOCIO.md).
4. Somente os próprios pedidos e o próprio briefing.
5. Só se explicitamente designado naquele pedido —
   [RN-BRF-030](06-REGRAS-DE-NEGOCIO.md).
6. Leitura da comissão vigente e do histórico. Alterar é da plataforma.
7. Somente leitura, e favoritar apenas se o estúdio habilitou.
8. Só com `SelecaoFechada` **e** saldo `Confirmado` —
   [RN-ENT-021](06-REGRAS-DE-NEGOCIO.md).
9. Só eventos do próprio tenant.

As três linhas em negrito são as que mais provavelmente serão implementadas
errado: `tenant.staff` no financeiro, `platform.admin` no briefing sensível e
convidado baixando original. Cada uma tem teste de autorização dedicado.

## 2. Autenticação

| Item | Decisão |
|---|---|
| Identidade | ASP.NET Core Identity, hash Argon2id ou PBKDF2 com custo atual do framework |
| Token de acesso | JWT, 15 min, claims `sub`, `tenant_id`, `role` |
| Token de renovação | opaco, 30 dias, **rotativo**: cada uso emite um novo e invalida o anterior |
| Reuso de refresh token | indício de roubo → invalida toda a família de tokens da sessão |
| Chave de assinatura | 64 bytes, do cofre do provedor. Rotação semestral com dois `kid` válidos na janela |
| Escopo | token é válido **só** no tenant do `tenant_id`. `TenantGuard` compara com o host — [RN-TEN-011](06-REGRAS-DE-NEGOCIO.md) |
| Senha | mínimo 10 caracteres; 5 falhas bloqueiam 15 min — [RN-TEN-010](06-REGRAS-DE-NEGOCIO.md) |
| Reset de senha | token de uso único, 1 h, escopado ao tenant. Enumeração de e-mail bloqueada: a resposta é sempre a mesma |
| Sessão de convidado | Redis, TTL 2 h, sem cookie permanente |
| MFA | fora da v1. Entra quando houver tenant com equipe grande |

## 3. Segredos

| Regra | Detalhe |
|---|---|
| Nunca no repositório | `.env` está no `.gitignore`, e o CI falha se `.env.example` tiver campo de segredo preenchido |
| Produção | cofre do provedor (Fly secrets, Key Vault, Vercel env). Nunca arquivo no container |
| Rotação | chave JWT semestral; chave do PSP e do storage a cada troca de pessoa com acesso, ou imediatamente em suspeita |
| Se um segredo aparecer em log ou em commit | trate como comprometido: **rotacione primeiro**, investigue depois. Apagar o commit não desfaz a exposição |
| Log e trace | nunca contêm `Authorization`, token, chave do PSP, string de conexão ou senha. Filtro explícito no Serilog |
| Chave do PSP | é segredo **da plataforma**, não do tenant. Um tenant nunca tem acesso a ela |

## 4. Inventário de dado pessoal

Base de tudo em LGPD: só se coleta o que tem finalidade
([RN-LGP-001](06-REGRAS-DE-NEGOCIO.md),
[RN-LGP-002](06-REGRAS-DE-NEGOCIO.md)). **Campo novo sem entrada nesta tabela
reprova o PR.**

| Dado | Onde | Finalidade | Base legal | Retenção |
|---|---|---|---|---|
| Nome, e-mail, WhatsApp do cliente | `client` | contato e execução do contrato | execução de contrato | 5 anos após conclusão |
| Data e local do evento | `order` | prestação do serviço | execução de contrato | 5 anos |
| Respostas de briefing **não** sensíveis | `briefing_answer` | direção do ensaio | execução de contrato | 5 anos |
| Respostas de briefing **sensíveis** (B3, B9, C2, C3, C4) | `briefing_answer` | acomodação e condução do ensaio | **consentimento específico** | **12 meses após a entrega** ([RN-LGP-004](06-REGRAS-DE-NEGOCIO.md)) |
| Consentimento do bloco sensível (escopo, IP, UA, finalidade) | `briefing_consent` | prova de consentimento do bloco B | consentimento | enquanto houver dado sensível + 5 anos |
| Destinatário e metadados de e-mail transacional (sem corpo sensível) | `notification_message` | idempotência e prova de envio ([RN-NOT-001](06-REGRAS-DE-NEGOCIO.md)) | legítimo interesse / execução de contrato | 12 meses |
| Staff designado para briefing sensível (user ids) | `order.sensitive_staff_user_ids` | autorização RN-BRF-030 | legítimo interesse | vida do pedido |
| Fotografias do cliente e de terceiros | storage + `photo` | entrega do serviço | execução de contrato | prazo contratual da galeria (padrão 12 meses), depois frio/arquivo |
| Consentimento de uso de imagem | `briefing_answer` D4 + `contract` | uso em portfólio e redes | consentimento | enquanto a imagem for usada + 5 anos |
| Consentimento do responsável por menor | `contract` | uso de imagem de menor | consentimento do responsável | idem |
| IP, user-agent, timestamp de assinatura | `signature` | prova de aceite | legítimo interesse / exercício de direito | 5 anos após conclusão |
| Documento e conta bancária do fotógrafo | **no PSP**, não aqui | KYC e recebimento | obrigação legal (PSP) | conforme o PSP |
| Últimos dígitos e bandeira do cartão | **no PSP**, não aqui | conferência da cobrança | execução de contrato | conforme o PSP |
| Trilha de auditoria | `audit_log` | segurança e prestação de contas | legítimo interesse / obrigação legal | 60 meses |
| Log de acesso a `ShareLink` | `share_link` | segurança da galeria | legítimo interesse | 12 meses |

**Dado que o Prata deliberadamente não guarda:** número de cartão, CVV,
documento do fotógrafo em claro, conta bancária em claro, diagnóstico de
saúde, e qualquer dado biométrico.

## 5. Dado pessoal sensível

Só o bloco B do briefing e alguns campos do bloco C produzem dado sensível.
As regras estão em [07 · Briefing](07-BRIEFING.md), seção 5. Resumo
operacional:

| Regra | ID |
|---|---|
| Pergunta é sobre acomodação, nunca diagnóstico | [RN-BRF-021](06-REGRAS-DE-NEGOCIO.md) |
| Bloco opcional, consentimento específico e separado do contrato | [RN-BRF-020](06-REGRAS-DE-NEGOCIO.md) |
| Visível só para o dono e para a equipe designada no pedido | [RN-BRF-030](06-REGRAS-DE-NEGOCIO.md) |
| Nunca em log, trace, métrica, erro, e-mail ou notificação | [RN-BRF-031](06-REGRAS-DE-NEGOCIO.md), [RN-LGP-005](06-REGRAS-DE-NEGOCIO.md) |
| Apagada 12 meses após a entrega, por job, com auditoria | [RN-LGP-004](06-REGRAS-DE-NEGOCIO.md) |
| Revogação do consentimento apaga as respostas | [RN-LGP-003](06-REGRAS-DE-NEGOCIO.md) |
| **Leitura** de resposta sensível gera registro de auditoria | [RN-AUD-003](06-REGRAS-DE-NEGOCIO.md) |

## 6. Imagem de menores

Risco alto e barato de mitigar. Três controles:

| Controle | Onde |
|---|---|
| Consentimento do responsável, registrado no contrato | [RN-CTR-002](06-REGRAS-DE-NEGOCIO.md), [RN-LGP-007](06-REGRAS-DE-NEGOCIO.md) |
| Pedido com menor identificado gera galeria com flag `has_minor` | `gallery.has_minor` |
| Galeria com `has_minor` fica **bloqueada para uso em portfólio**, independente da resposta D4 | [RN-ENT-031](06-REGRAS-DE-NEGOCIO.md) |

O terceiro é o que importa: um "sim" no D4 dado por quem não é responsável
legal não autoriza nada, e o sistema não deve permitir que essa distinção
dependa da memória do fotógrafo.

## 7. Direitos do titular

| Direito | Como se atende | Nota |
|---|---|---|
| Confirmação e acesso | exportação em formato legível, por URL assinada de TTL curto | [RN-LGP-008](06-REGRAS-DE-NEGOCIO.md) |
| Correção | edição no portal do cliente | — |
| Anonimização / eliminação | remove contato e briefing; **preserva** registro fiscal e contratual pelo prazo legal, anonimizando o que for possível | [RN-LGP-006](06-REGRAS-DE-NEGOCIO.md) |
| Revogação de consentimento | apaga respostas sensíveis e revoga uso de imagem no portfólio | [RN-LGP-003](06-REGRAS-DE-NEGOCIO.md) |
| Portabilidade | mesma exportação do acesso | — |
| Oposição | canal de contato do controlador | — |

**Quem é o controlador.** O fotógrafo (tenant) é controlador do dado dos
clientes dele; o Prata é **operador**. Isso precisa estar no termo de uso e
muda quem responde ao titular: o pedido chega ao fotógrafo, e a plataforma
fornece a ferramenta. Confirmar o enquadramento com advogado — há leitura de
controladoria conjunta em alguns pontos, como a trilha de auditoria.

## 8. Superfícies de ataque e mitigação

Em ordem de impacto.

| # | Vetor | Mitigação |
|---|---|---|
| 1 | Vazamento entre tenants | três barreiras + teste obrigatório. [03 · Multi-tenancy](03-MULTI-TENANCY.md) |
| 2 | Webhook forjado marcando cobrança como paga | assinatura verificada antes de gravar — [RN-FIN-020](06-REGRAS-DE-NEGOCIO.md) |
| 3 | Enumeração de `ShareLink` | token assinado não sequencial + senha + rate limit de 20 req/min |
| 4 | URL assinada compartilhada indefinidamente | TTL de 15 min |
| 5 | Escalação de `tenant.staff` para financeiro | autorização por papel em cada endpoint + teste dedicado |
| 6 | Enumeração de e-mail no login e no reset | resposta idêntica em todos os casos, com tempo constante |
| 7 | Upload de arquivo malicioso | validação de tipo real (magic bytes, não extensão), limite de tamanho, storage sem execução, sem servir `Content-Type` vindo do cliente |
| 8 | XSS via campo livre do briefing | escape na renderização; `Content-Security-Policy` restritiva; nada de `dangerouslySetInnerHTML` em conteúdo de cliente |
| 9 | SSRF via campo de URL de referência (D2) | não buscar a URL no servidor. Guardar como texto e abrir no navegador do usuário |
| 10 | Abuso de geração de ZIP | idempotência por escopo + limite por tenant |
| 11 | Injeção de SQL nas queries Dapper | parâmetro sempre; concatenação de string proibida, verificada em revisão |
| 12 | Chave de idempotência descartada por pressão de memória no Redis | `maxmemory-policy noeviction` — ver [`docker-compose.yml`](../docker-compose.yml) |

### Cabeçalhos obrigatórios na resposta pública

```
Strict-Transport-Security: max-age=63072000; includeSubDomains
Content-Security-Policy: default-src 'self'; img-src 'self' data: <cdn>; ...
X-Content-Type-Options: nosniff
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: camera=(), microphone=(), geolocation=()
```

A CSP precisa acomodar o CDN de imagem e a camada WebGL sem abrir
`unsafe-inline` para script. Ver
[16 · Front-end e experiência](16-FRONTEND-E-EXPERIENCIA.md).

## 9. Resposta a incidente

Roteiro mínimo, para existir antes de precisar.

| Passo | Ação |
|---|---|
| 1 | Conter: revogar credencial exposta, suspender tenant afetado se necessário |
| 2 | Preservar: exportar `audit_log` e log do período **antes** de qualquer correção |
| 3 | Dimensionar: quais tenants, quais titulares, qual categoria de dado |
| 4 | Corrigir a causa |
| 5 | Comunicar: se houver risco relevante ao titular, notificar ANPD e os titulares em prazo razoável |
| 6 | Registrar: post-mortem sem culpado, com a mudança de controle que impede a repetição |

O passo 2 é o que se esquece na pressa — e é o único que não dá para refazer
depois.
