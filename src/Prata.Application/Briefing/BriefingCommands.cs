using Prata.Application.Abstractions;
using Prata.Application.Common;
using Prata.Domain.Briefing;
using Prata.Domain.Common;
using Prata.Domain.Sales;

namespace Prata.Application.Briefing;

public interface IBriefingTemplateRepository
{
    Task<BriefingTemplate?> GetPublishedForServiceAsync(
        Guid tenantId,
        Guid serviceTypeId,
        CancellationToken cancellationToken = default
    );

    Task<BriefingTemplate?> GetByIdAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BriefingTemplate>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(BriefingTemplate template, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<BriefingTemplate> templates, CancellationToken cancellationToken = default);
}

public interface IAnswerRepository
{
    Task<IReadOnlyList<Answer>> ListByOrderAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken = default);

    Task<Answer?> GetByQuestionAsync(
        Guid tenantId,
        Guid orderId,
        string questionCode,
        CancellationToken cancellationToken = default
    );

    Task UpsertAsync(Answer answer, CancellationToken cancellationToken = default);

    Task RemoveSensitiveByOrderAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken = default);

    Task<int> PurgeSensitiveDeliveredBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}

public interface IBriefingConsentRepository
{
    Task<BriefingConsent?> GetActiveAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken = default);

    Task AddAsync(BriefingConsent consent, CancellationToken cancellationToken = default);
}

public static class BriefingPurpose
{
    public const string DefaultText =
        "Estas perguntas sao opcionais e servem so para o fotografo conduzir melhor o seu ensaio. "
        + "So quem vai fotografar voce tem acesso. Voce pode deixar em branco ou apagar depois, sem prejuizo ao pedido.";
}

public sealed record AutosaveAnswerCommand(Guid OrderId, string QuestionCode, string ValueJson) : ICommand<Unit>;

public sealed class AutosaveAnswerHandler(
    ITenantContext tenantContext,
    IOrderRepository orders,
    IBriefingTemplateRepository templates,
    IAnswerRepository answers,
    IBriefingConsentRepository consents,
    IUnitOfWork uow
) : ICommandHandler<AutosaveAnswerCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AutosaveAnswerCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        if (order.Status is not (OrderStatus.Rascunho or OrderStatus.Enviado))
            return Error.Validation("BRIEFING_PEDIDO_FECHADO", "Briefing so aceita autosave em Rascunho ou Enviado.");

        var template = await templates.GetPublishedForServiceAsync(tenantId, order.ServiceTypeId, cancellationToken);
        if (template is null)
            return Error.Validation("BRIEFING_TEMPLATE_AUSENTE", "Template de briefing nao encontrado.");

        var code = command.QuestionCode.Trim().ToUpperInvariant();
        var question = template.Questions.FirstOrDefault(q => q.Code == code);
        if (question is null)
            return Error.Validation("BRIEFING_PERGUNTA_AUSENTE", "Pergunta nao encontrada no template.");

        var existingAnswers = await answers.ListByOrderAsync(tenantId, order.Id, cancellationToken);
        var byCode = existingAnswers
            .Where(a => !string.Equals(a.QuestionCode, code, StringComparison.Ordinal))
            .ToDictionary(a => a.QuestionCode, StringComparer.Ordinal);

        if (!VisibleWhenEvaluator.IsVisible(question, byCode))
            return Error.Validation("BRIEFING_PERGUNTA_OCULTA", "Pergunta nao visivel pelo condicional atual.");

        var shape = AnswerValueValidator.Validate(question.Type, command.ValueJson);
        if (shape.IsFailure)
            return shape;

        var consent = await consents.GetActiveAsync(tenantId, order.Id, cancellationToken);
        var created = Answer.Create(
            tenantId,
            order.Id,
            question,
            command.ValueJson,
            valueShapeValid: true,
            hasSensitiveConsent: consent is not null
        );
        if (created.IsFailure)
            return Result.Failure<Unit>(created.Error!.Value);

        await answers.UpsertAsync(created.Value, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RegisterBriefingConsentCommand(Guid OrderId, string Ip, string UserAgent, string? PurposeText)
    : ICommand<Unit>;

public sealed class RegisterBriefingConsentHandler(
    ITenantContext tenantContext,
    IDateTimeProvider clock,
    IOrderRepository orders,
    IBriefingConsentRepository consents,
    IUnitOfWork uow
) : ICommandHandler<RegisterBriefingConsentCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RegisterBriefingConsentCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var existing = await consents.GetActiveAsync(tenantId, order.Id, cancellationToken);
        if (existing is not null)
            return Unit.Value;

        var purpose = string.IsNullOrWhiteSpace(command.PurposeText)
            ? BriefingPurpose.DefaultText
            : command.PurposeText!;

        var created = BriefingConsent.Create(
            tenantId,
            order.Id,
            clock.UtcNow,
            command.Ip,
            command.UserAgent,
            purpose
        );
        if (created.IsFailure)
            return Result.Failure<Unit>(created.Error!.Value);

        await consents.AddAsync(created.Value, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RevokeBriefingConsentCommand(Guid OrderId) : ICommand<Unit>;

public sealed class RevokeBriefingConsentHandler(
    ITenantContext tenantContext,
    IDateTimeProvider clock,
    IBriefingConsentRepository consents,
    IAnswerRepository answers,
    IUnitOfWork uow
) : ICommandHandler<RevokeBriefingConsentCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RevokeBriefingConsentCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var consent = await consents.GetActiveAsync(tenantId, command.OrderId, cancellationToken);
        if (consent is null)
            return Error.Validation("BRIEFING_CONSENTIMENTO_AUSENTE", "Nao ha consentimento ativo.");

        var revoke = consent.Revogar(clock.UtcNow);
        if (revoke.IsFailure)
            return revoke;

        await answers.RemoveSensitiveByOrderAsync(tenantId, command.OrderId, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
