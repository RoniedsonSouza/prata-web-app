# 06 · Catálogo de regras de negócio

Cada regra tem **ID estável**, enunciado verificável e como testar. O ID é
citado em nome de teste, em comentário de código e nos outros documentos —
por isso **nunca se renumera**. Regra revogada fica na tabela com o enunciado
riscado e a nota `Revogada por RN-XXX-000`.

O CI valida que toda `RN-XXX-000` citada em qualquer documento está definida
aqui (`./scripts/check-docs.sh`).

## Convenção

| Faixa | Assunto |
|---|---|
| `001`–`009` | fundação e estrutura |
| `010`–`019` | entrada e criação |
| `020`–`029` | núcleo do fluxo |
| `030`–`039` | bloqueio e guarda |
| `040`–`049` | cancelamento e exceção |
| `050`+ | encerramento e ciclo de vida |

Prefixos: `TEN` identidade · `CAT` catálogo · `VIT` vitrine · `COM` comercial
· `BRF` briefing · `AGD` agenda · `FIN` financeiro · `ENT` entrega ·
`CTR` contrato · `NOT` notificação · `AUD` auditoria · `LGP` LGPD ·
`FRT` front-end.

---

## TEN · Identidade, tenant e acesso

| ID | Regra | Como testar |
|---|---|---|
| `RN-TEN-001` | Toda tabela de dado de negócio tem `tenant_id uuid NOT NULL`, sem default, e a entidade implementa `ITenantOwned` | Teste de schema varre `information_schema` e falha se achar tabela de negócio sem a coluna |
| `RN-TEN-002` | Nenhuma leitura ou escrita de dado de negócio ocorre sem filtro de tenant, garantido por três barreiras: query filter do EF, RLS no Postgres e `TenantId` explícito em repositório e Dapper | `TenantIsolationTests`: para cada tabela, ler e escrever cruzado entre dois tenants **precisa falhar** |
| `RN-TEN-003` | Slug do tenant é único global, casa `^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$`, não está na lista de reservados e é **imutável** após a primeira publicação do portfólio | Teste parametrizado com slugs válidos, inválidos e reservados; tentativa de alteração pós-publicação retorna erro |
| `RN-TEN-004` | Chave única de usuário é `(tenant_id, email)`. O mesmo e-mail pode existir em N tenants, com senhas independentes | Criar o mesmo e-mail em dois tenants tem sucesso; duplicar no mesmo tenant falha |
| `RN-TEN-005` | `platform.admin` não acessa resposta de briefing nem arquivo de foto de nenhum tenant — só metadado e agregado | Teste de autorização: `platform.admin` recebe `403` em `/orders/{id}/briefing` e em URL assinada de foto |
| `RN-TEN-006` | `tenant.staff` não lê nem escreve nada de Financeiro, `PayoutAccount`, `SplitRule` ou `Payout` | Todo endpoint financeiro retorna `403` para `tenant.staff` |
| `RN-TEN-007` | Um tenant tem sempre pelo menos um `tenant.owner` ativo. Remover ou rebaixar o último é bloqueado | Remover o único owner retorna erro `UltimoOwner` |
| `RN-TEN-008` | Convite expira em 7 dias. O aceite cria `User` no tenant do convite, e apenas nele | Aceitar convite com 8 dias falha; aceite válido cria usuário com `tenant_id` do convite |
| `RN-TEN-009` | Tenant `Suspenso`: portfólio público responde `503` com página de manutenção, back-office fica somente leitura, galeria já entregue continua acessível ao cliente | Suspender tenant e verificar os três comportamentos |
| `RN-TEN-010` | Senha tem no mínimo 10 caracteres; 5 tentativas falhas bloqueiam a conta por 15 minutos | Teste de política e de lockout |
| `RN-TEN-011` | Token JWT carrega `tenant_id` e `role`. Requisição cujo `tenant_id` do token difere do tenant resolvido pelo host é rejeitada com `403` | Token do tenant A em host do tenant B retorna `403` |
| `RN-TEN-012` | A API valida no boot que está conectada com papel `NOBYPASSRLS` e **se recusa a subir** se estiver como dono das tabelas | Subir a API com a string de migration falha o health check de inicialização |

## CAT · Catálogo de serviços e pacotes

| ID | Regra | Como testar |
|---|---|---|
| `RN-CAT-001` | Preço de pacote é `Money` em BRL, maior que zero, com duas casas decimais | Criar pacote com 0, negativo ou 3 casas falha |
| `RN-CAT-002` | Pacote pertence a exatamente um `ServiceType` do mesmo tenant | Vincular pacote a serviço de outro tenant falha |
| `RN-CAT-003` | Pacote só é publicável com nome, preço, nº de fotos incluídas e ≥ 1 entregável | Publicar pacote incompleto falha, listando os campos ausentes |
| `RN-CAT-004` | Nome e preço são **copiados** para `OrderItem` na montagem do orçamento. Alterar tabela de preço nunca altera pedido existente | Criar pedido, alterar preço do pacote, reler pedido: total inalterado |
| `RN-CAT-005` | Desativar pacote impede novo pedido e **não** afeta pedido em andamento | Desativar com pedido ativo: pedido segue; novo pedido com o pacote falha |
| `RN-CAT-006` | Adicional tem preço próprio e pode ser marcado como "sob consulta", sem valor | Adicional sob consulta entra no pedido sem somar ao total e exige orçamento manual |

## VIT · Vitrine e portfólio público

| ID | Regra | Como testar |
|---|---|---|
| `RN-VIT-001` | Coleção só é publicável com ≥ 1 item e capa definida | Publicar coleção vazia ou sem capa falha |
| `RN-VIT-002` | Slug de coleção é único por tenant e válido para URL | Duplicar slug no mesmo tenant falha; duplicar entre tenants tem sucesso |
| `RN-VIT-003` | A ordem dos itens é explícita (`SortOrder`), nunca implícita por data de upload | Reordenar persiste e a API pública devolve na ordem definida |
| `RN-VIT-004` | Publicar, despublicar ou reordenar dispara revalidação de ISR da rota do tenant | Evento de publicação chama o endpoint de revalidação com a tag correta |
| `RN-VIT-005` | Foto cujo consentimento de portfólio é `Nao` **nunca** entra em coleção pública. Consentimento `SomenteSemRosto` exige marcação manual e revisão | Tentar adicionar item com consentimento `Nao` falha com `ConsentimentoAusente` |
| `RN-VIT-006` | Toda página pública tem `title`, `meta description`, imagem de OG e URL canônica por tenant | Teste de contrato na resposta da rota pública |
| `RN-VIT-007` | `sitemap.xml` e `robots.txt` são gerados por tenant, listando só coleções publicadas | Sitemap de um tenant não contém rota de outro |
| `RN-VIT-008` | Imagem de portfólio é servida sem marca d'água — a curadoria é do fotógrafo | Derivada de vitrine não tem a camada de marca d'água |

## COM · Comercial, pedidos e orçamento

| ID | Regra | Como testar |
|---|---|---|
| `RN-COM-001` | Total do pedido = Σ `OrderItem` − desconto. Recalculado apenas entre `Rascunho` e `OrcamentoEnviado` | Alterar item em `Aprovado` falha; em `Rascunho` recalcula |
| `RN-COM-002` | Desconto nunca excede o subtotal, e desconto percentual e fixo não se acumulam na mesma linha | Desconto maior que subtotal falha |
| `RN-COM-010` | Data pretendida não pode estar no passado no momento da criação | Criar pedido com data de ontem falha |
| `RN-COM-011` | `Rascunho → Enviado` exige todas as perguntas obrigatórias do briefing respondidas | Enviar com obrigatória vazia falha listando as pendências |
| `RN-COM-012` | Cliente é identificado por `(tenant_id, email)`; pedido novo do mesmo e-mail reusa o `Client` existente | Segundo pedido não cria `Client` duplicado |
| `RN-COM-013` | Orçamento tem validade em dias, padrão 7, configurável por tenant. `ValidoAte` é gravado no `Quote`, não calculado na leitura | Alterar a configuração do tenant não muda a validade de orçamento já enviado |
| `RN-COM-020` | `Aprovado → Confirmado` exige sinal `Confirmado` **e** contrato `Assinado`. Não existe caminho manual; nem `tenant.owner` pode forçar | Confirmar sem uma das duas condições falha. Endpoint de confirmação manual não existe |
| `RN-COM-021` | A reavaliação para `Confirmado` é idempotente e disparada por `PagamentoConfirmado` **e** por `ContratoAssinado`, em qualquer ordem | Entregar os eventos nas duas ordens e em duplicata produz exatamente uma confirmação |
| `RN-COM-022` | Revisar orçamento em `OrcamentoEnviado` reinicia a validade e registra a versão anterior | Revisar gera nova validade e mantém histórico consultável |
| `RN-COM-030` | Toda transição inválida retorna erro `TransicaoInvalida` com estado de origem e destino, sem alterar a entidade | Teste parametrizado percorre todos os pares estado×transição |
| `RN-COM-031` | `EmEspera` exige motivo e guarda o estado de origem para retomada | Retomar volta exatamente ao estado anterior |
| `RN-COM-040` | `CanceladoPeloCliente` aplica a política de reembolso por janela: até 30 dias do evento devolve o sinal integral; entre 30 e 7 dias devolve 50%; a menos de 7 dias o sinal não é devolvido. A janela é configurável por tenant, e o valor calculado é registrado no cancelamento | Teste parametrizado com as três janelas verifica o valor de estorno gerado |
| `RN-COM-041` | `CanceladoPeloEstudio` gera reembolso integral por padrão, incluindo sinal, e exige motivo | Cancelar pelo estúdio cria estorno do total confirmado |
| `RN-COM-042` | `Reagendado` mantém pedido, pagamentos e contrato; libera a data antiga e reserva a nova | Reagendar não cria pedido novo e não estorna pagamento |
| `RN-COM-050` | `EmEdicao → Entregue` exige galeria `Disponivel` **e** saldo `Confirmado` | Entregar com saldo pendente falha |
| `RN-COM-051` | `Concluido` vem de job quando a seleção fechou e o download foi feito, ou quando o prazo de seleção venceu | Job de encerramento move o pedido nas duas condições |

## BRF · Briefing e direção

| ID | Regra | Como testar |
|---|---|---|
| `RN-BRF-001` | `BriefingTemplate` pertence a um par `(tenant, ServiceType)`. Um tenant sem template usa o template semente da plataforma, por cópia — nunca por referência | Criar tenant copia os templates semente; editar não afeta outros tenants |
| `RN-BRF-002` | `Answer` guarda o **snapshot do texto da pergunta** no momento da resposta. Editar o template não altera briefing já respondido | Responder, editar a pergunta, reler: o texto original permanece |
| `RN-BRF-003` | `VisibleWhen` só referencia pergunta anterior no mesmo template. Referência circular ou futura é rejeitada na publicação | Publicar template com dependência circular falha |
| `RN-BRF-004` | Resposta é gravada como `jsonb` tipado pelo `QuestionType`. Tipo divergente é rejeitado | Enviar texto onde se espera escala falha |
| `RN-BRF-010` | Pergunta `IsRequired` precisa de resposta não vazia antes de `Rascunho → Enviado`, respeitando o condicional: pergunta oculta por `VisibleWhen` não é exigida | Obrigatória oculta não bloqueia o envio |
| `RN-BRF-011` | Autosave grava resposta parcial sem validar obrigatoriedade | `PUT` parcial sucede com campos faltando |
| `RN-BRF-020` | Todo o bloco B (direção e conforto) é **opcional** e exige consentimento específico registrado, com finalidade declarada em texto simples ao lado | Responder bloco B sem consentimento falha |
| `RN-BRF-021` | Pergunta sobre corpo, mobilidade ou saúde é proibida no formato de diagnóstico. Só se pergunta a **acomodação operacional** ("precisa de pausas?", "evitar escadas?", "evitar sol forte?") | Revisão de template: pergunta marcada `IsSensitive` que peça causa é rejeitada na publicação |
| `RN-BRF-030` | Resposta marcada `IsSensitive` é visível apenas para `tenant.owner` e `tenant.staff` explicitamente designado no pedido. Nunca para `platform.admin` | Teste de autorização nas três combinações |
| `RN-BRF-031` | Resposta sensível nunca aparece em log, trace, mensagem de erro, e-mail ou notificação — só a referência ao pedido | Teste de log: capturar saída durante o fluxo e afirmar ausência do conteúdo |
| `RN-BRF-040` | A ficha de direção em PDF cabe em **uma página**, é gerada sob demanda e nunca é anexada em e-mail — é servida por URL assinada de TTL curto | Gerar ficha de briefing extenso ainda produz uma página; e-mail contém link, não anexo |

## AGD · Agenda e disponibilidade

| ID | Regra | Como testar |
|---|---|---|
| `RN-AGD-001` | Não existem dois `Booking` ativos sobrepostos no mesmo tenant, considerando o buffer de deslocamento configurado | Criar reserva sobreposta falha, inclusive dentro do buffer |
| `RN-AGD-002` | `Booking` só nasce de pedido `Confirmado` | Criar reserva a partir de pedido `Aprovado` falha |
| `RN-AGD-010` | `BlackoutDate` impede criação de `Booking` na data. Bloquear data que já tem reserva ativa falha | Testar as duas direções |
| `RN-AGD-020` | Cancelar ou reagendar pedido libera a data imediatamente | Após cancelar, a data volta a aceitar reserva |
| `RN-AGD-030` | Enquanto o módulo de Agenda não existir (antes da E5), a checagem de conflito de data acontece no Comercial contra os pedidos `Confirmado` do tenant | Teste no Comercial impede duas confirmações na mesma data |

## FIN · Financeiro, pagamento e repasse

| ID | Regra | Como testar |
|---|---|---|
| `RN-FIN-001` | A plataforma **nunca** recebe o valor cheio para repassar depois. Todo pagamento nasce com regra de split e o dinheiro cai dividido na origem | Contrato do adapter: criar cobrança sem `SplitRule` falha |
| `RN-FIN-002` | Dado de cartão **nunca** é armazenado, logado ou trafegado pela API. Só token do PSP, via checkout transparente | Teste de contrato: payload de cobrança de cartão não aceita PAN, CVV nem validade |
| `RN-FIN-003` | Valor monetário é `numeric(14,2)` no banco e `Money` no domínio. `double` é proibido em qualquer caminho de dinheiro | Analisador/teste de arquitetura falha ao encontrar `double` em `Domain/Billing` |
| `RN-FIN-004` | Σ das parcelas é exatamente igual ao total do pagamento. A diferença de arredondamento vai na **última** parcela | Dividir 100,00 em 3 gera 33,33 + 33,33 + 33,34 |
| `RN-FIN-010` | Sinal está entre 30% e 50% do total, configurável por tenant dentro dessa faixa | Sinal de 20% ou 60% falha |
| `RN-FIN-011` | Saldo vence N dias antes da data do evento, padrão 7, configurável por tenant | Criar pagamento gera vencimento coerente com a data do evento |
| `RN-FIN-012` | Cobrança do sinal é criada em reação a `OrcamentoAprovado`, não por ação manual do cliente | Aprovar orçamento cria `Payment` do tipo `Deposit` |
| `RN-FIN-020` | Todo webhook do PSP tem a assinatura verificada antes de qualquer processamento. Assinatura inválida retorna `401` e **não** grava evento | Enviar webhook com assinatura forjada não altera estado nenhum |
| `RN-FIN-021` | Webhook é idempotente por `PaymentEvent.ExternalEventId`, com índice único. Reenvio é aceito com `200` e ignorado | Entregar o mesmo evento 3 vezes produz uma transição |
| `RN-FIN-022` | Estado de pagamento muda **somente** por webhook verificado ou consulta ativa à API do PSP. Retorno do navegador nunca altera estado | Chamar o endpoint de retorno com sucesso falso não confirma pagamento |
| `RN-FIN-023` | Webhook fora de ordem não regride estado: evento mais antigo que o estado atual é registrado e descartado | Entregar `Liquidado` e depois `Confirmado` mantém `Liquidado` |
| `RN-FIN-030` | Repasse é bloqueado enquanto o KYC do fotógrafo não estiver `Aprovado`. O bloqueio aparece no back-office do fotógrafo, não só no log da plataforma | Liquidar com KYC pendente cria `Payout` em `BloqueadoKyc` e o painel do estúdio exibe o aviso |
| `RN-FIN-031` | Aprovação de KYC dispara reprocessamento automático dos `Payout` em `BloqueadoKyc` | Aprovar KYC agenda os repasses retidos |
| `RN-FIN-032` | `SplitRule` aplicada é **snapshot** gravado na criação da cobrança. Alterar a comissão do tenant nunca reescreve cobrança existente | Criar cobrança, mudar a taxa, reler: comissão original preservada |
| `RN-FIN-033` | `SplitRule` é versionada com `VigenteDe`/`VigenteAte`. Não existe `UPDATE` destrutivo de comissão | Alterar comissão cria nova versão e encerra a anterior |
| `RN-FIN-040` | Estorno respeita a política de cancelamento aplicável e registra o valor calculado, o motivo e o ator | Estornar gera `AuditLog` com os três campos |
| `RN-FIN-041` | Chargeback move o pagamento para `EmDisputa`, retém o repasse correspondente e notifica o `tenant.owner` | Webhook de chargeback produz os três efeitos |
| `RN-FIN-050` | Job diário de conciliação compara `Payment` local com o extrato do PSP e abre `DivergenciaDeConciliacao` para cada diferença de valor ou de estado | Semear divergência e verificar que o job a detecta e alarma |
| `RN-FIN-051` | Nenhuma divergência de conciliação é resolvida automaticamente. Toda baixa é manual, com registro de quem fez | Endpoint de baixa exige ator autenticado e grava auditoria |

## ENT · Entrega e galerias

| ID | Regra | Como testar |
|---|---|---|
| `RN-ENT-001` | Upload vai do navegador direto para o storage por URL pré-assinada, com multipart. O arquivo **nunca** passa pela API | Endpoint de upload devolve URL assinada e não aceita corpo binário |
| `RN-ENT-002` | Nenhum bucket e nenhum objeto tem ACL público. Toda leitura é por URL assinada com TTL curto | Teste de infra verifica ACL dos buckets; objeto acessado sem assinatura retorna `403` |
| `RN-ENT-003` | O original nunca é alterado, reprocessado nem sobrescrito. Derivada nova é arquivo novo | Regerar derivadas não muda o hash do original |
| `RN-ENT-004` | A URL assinada é emitida **depois** da autorização do recurso, nunca antes | Solicitar URL de foto de outro tenant retorna `403` sem gerar assinatura |
| `RN-ENT-010` | `EmPreparo → Disponivel` exige derivada `thumb` e `web` geradas para **todas** as fotos da galeria | Disponibilizar com uma derivada pendente falha |
| `RN-ENT-011` | Derivada padrão: `thumb` 480px, `web` 1600px com marca d'água, `texture` 1024px para o portfólio, `lqip` 20px embutido | Teste do processador verifica as quatro saídas e as dimensões |
| `RN-ENT-020` | A seleção respeita o limite de favoritas do pacote. O excedente gera `Payment` de upsell, e a seleção só fecha com esse pagamento `Confirmado` | Selecionar acima do limite sem pagar falha; com upsell confirmado sucede |
| `RN-ENT-021` | O original só é liberado com galeria `SelecaoFechada` **e** saldo `Confirmado` | Baixar original antes das duas condições retorna `403` |
| `RN-ENT-030` | `PortfolioConsent` da galeria é herdado da resposta do briefing (bloco D) e bloqueia uso no portfólio quando `Nao` | Responder `Nao` e tentar publicar item da galeria falha |
| `RN-ENT-031` | Havendo menor de idade identificado no pedido, a galeria recebe flag que **bloqueia** uso em portfólio e redes, independente da resposta do bloco D | Pedido com menor gera galeria bloqueada para portfólio |
| `RN-ENT-032` | Galeria com parcela vencida não paga vai para `BloqueadaPorPendencia`: abre em baixa resolução com marca d'água, download em alta travado | Vencer parcela e verificar os dois efeitos; confirmar pagamento restaura |
| `RN-ENT-040` | `ShareLink` tem token assinado, senha de no mínimo 6 caracteres e expiração. Convidado não cria conta e tem acesso somente leitura, com favoritar opcional | Acesso sem senha, com senha errada ou expirado retorna `403`; convidado não consegue baixar original |
| `RN-ENT-041` | O ZIP é montado em background e notificado por e-mail. Nunca dentro do request | Endpoint de download devolve `202` com id do job |
| `RN-ENT-042` | `DownloadJob` tem TTL. Depois disso o ZIP é apagado do storage de export | Job de limpeza remove ZIP vencido |
| `RN-ENT-050` | Galeria expira em N meses, padrão 12, e o prazo consta no contrato. Aviso automático em 30, 7 e 1 dia antes | Job de expiração emite os três avisos e move para `Expirada` |
| `RN-ENT-051` | Galeria `Expirada` vai para storage frio; `Arquivada` é recuperável sob demanda pelo `tenant.owner`, com custo | Reativar galeria arquivada registra a cobrança de reativação |

## CTR · Contrato e assinatura

| ID | Regra | Como testar |
|---|---|---|
| `RN-CTR-001` | O contrato é gerado de template do tenant e o PDF tem hash SHA-256 calculado antes do envio | Enviar sem hash falha |
| `RN-CTR-002` | O contrato precisa conter cláusula de prazo de expiração da galeria, consentimento de uso de imagem e, havendo menor, consentimento do responsável | Teste de template verifica a presença das três cláusulas |
| `RN-CTR-010` | `Signature` registra hash do PDF, IP, user-agent e timestamp com fuso. Assinatura sem qualquer um dos quatro é inválida | Assinar sem IP falha |
| `RN-CTR-011` | Contrato `Assinado` é **imutável**. Correção gera contrato novo com referência ao anterior | Editar assinado falha; corrigir cria novo com `SubstituiContratoId` |
| `RN-CTR-020` | Pedido com sinal `Confirmado` e contrato não assinado é pendência acionável no painel do fotógrafo, e o job de lembrete cobra a assinatura em 1, 3 e 7 dias | Estado aparece na lista de pendências e os três lembretes são emitidos |
| `RN-CTR-030` | Contrato expira conforme validade do tenant. Expirado não pode ser assinado | Assinar contrato expirado falha |

## NOT · Notificações

| ID | Regra | Como testar |
|---|---|---|
| `RN-NOT-001` | Notificação é idempotente por `(tenant, destinatário, tipo, chave de origem)`. Job que roda duas vezes não envia dois e-mails | Executar o job duas vezes gera um envio |
| `RN-NOT-002` | Nenhuma notificação carrega resposta sensível de briefing no corpo, assunto ou anexo — apenas link autenticado | Teste de conteúdo do e-mail afirma ausência do dado sensível |
| `RN-NOT-003` | Toda tentativa de envio registra canal, destinatário, resultado e erro. Falha de envio não desfaz a transação de negócio | Simular falha de SMTP: o pedido segue confirmado e a notificação fica com falha registrada |
| `RN-NOT-010` | Na v1 o WhatsApp é link `wa.me` pré-preenchido, gerado no back-office e disparado por ação humana. Nenhum envio automático por WhatsApp | Não existe caminho de código que envie WhatsApp sem ação do usuário |
| `RN-NOT-011` | E-mail transacional tem remetente do tenant no `From` amigável e domínio da plataforma no envelope, para não quebrar SPF/DKIM | Cabeçalho do e-mail verificado em teste de integração |

## AUD · Auditoria

| ID | Regra | Como testar |
|---|---|---|
| `RN-AUD-001` | `AuditLog` é append-only. `UPDATE` e `DELETE` são bloqueados por policy no banco, não só por convenção de código | Tentar atualizar linha de auditoria falha no banco |
| `RN-AUD-002` | Toda operação que toca dinheiro registra auditoria: criação de cobrança, transição de pagamento, estorno, alteração de `SplitRule`, cadastro de `PayoutAccount`, baixa de conciliação | Teste percorre a lista e afirma um registro por operação |
| `RN-AUD-003` | Toda operação que toca dado pessoal registra auditoria: leitura de briefing sensível, exportação de dado do cliente, exclusão a pedido do titular | Idem |
| `RN-AUD-004` | O registro contém ator, papel, tenant, recurso, ação, valor antes, valor depois, IP e timestamp. Nunca contém o **conteúdo** de resposta sensível — só a referência | Teste de forma do registro |

## LGP · LGPD e privacidade

| ID | Regra | Como testar |
|---|---|---|
| `RN-LGP-001` | Todo dado pessoal coletado tem finalidade declarada e base legal registrada no inventário de [12 · Segurança e LGPD](12-SEGURANCA-E-LGPD.md) | Revisão: campo novo sem entrada no inventário reprova o PR |
| `RN-LGP-002` | Só se coleta o necessário para executar o serviço. Campo sem uso definido não entra no formulário | Idem |
| `RN-LGP-003` | Consentimento do bloco sensível é específico, separado do aceite do contrato, revogável, e a revogação apaga as respostas | Revogar consentimento remove as respostas sensíveis e mantém o pedido |
| `RN-LGP-004` | Resposta sensível de briefing é apagada automaticamente 12 meses após a entrega do pedido, por job, com registro em auditoria | Job de expurgo remove as respostas e deixa rastro |
| `RN-LGP-005` | Dado pessoal sensível não entra em log, trace, métrica, mensagem de erro nem e-mail, em nenhum ambiente | Teste de log em cima do fluxo completo do briefing |
| `RN-LGP-006` | Exclusão a pedido do titular remove dado de contato e briefing, e **preserva** registro fiscal e contratual pelo prazo legal, com anonimização do que for possível | Solicitar exclusão remove contato e mantém `Payment` anonimizado |
| `RN-LGP-007` | Imagem de menor exige consentimento do responsável registrado no contrato, e a galeria correspondente é bloqueada para uso em portfólio | Ver `RN-ENT-031` |
| `RN-LGP-008` | Exportação de dado do titular sai em formato legível, por URL assinada de TTL curto, com auditoria | Solicitar exportação gera arquivo e registro |

## FRT · Front-end e experiência

| ID | Regra | Como testar |
|---|---|---|
| `RN-FRT-001` | A rota pública entrega no máximo 120 kB de JS gzip **sem** a camada WebGL. O chunk WebGL tem orçamento próprio de 200 kB e carrega só depois do LCP | `size-limit` no CI quebra o build ao estourar |
| `RN-FRT-002` | LCP ≤ 2,0 s, INP ≤ 200 ms e CLS ≤ 0,05 em mobile emulado, medidos no CI | Lighthouse CI com as assertions de `.github/lighthouse/` |
| `RN-FRT-003` | Nenhum conteúdo indexável vive dentro do `canvas`. Toda foto do portfólio existe como `<img>` real no DOM | Teste de HTML renderizado conta as imagens sem JS |
| `RN-FRT-004` | A camada WebGL só monta se passar em `prefers-reduced-motion: no-preference`, ausência de `save-data`, `deviceMemory ≥ 4`, `hardwareConcurrency ≥ 4`, criação de contexto WebGL2 e flag `EffectsEnabled` do tenant | Teste com cada condição negada verifica que o canvas não monta e a página continua correta |
| `RN-FRT-005` | O smooth scroll (Lenis) é desativado em dispositivo de toque e sob `prefers-reduced-motion` | Teste de comportamento nos dois casos |
| `RN-FRT-006` | Portal do cliente e back-office não têm WebGL, scroll hijack nem animação de entrada de conteúdo | Revisão de PR e teste que falha se a lib de WebGL for importada nesses grupos de rota |
| `RN-FRT-007` | Falha na camada WebGL degrada silenciosamente para a versão DOM, sem erro visível e sem tela em branco | Forçar erro de shader mantém a página funcional |
