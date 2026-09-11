using System.Text.Json;
using Prata.Application.Abstractions;
using Prata.Application.Common;
using Prata.Domain.Briefing;
using Prata.Domain.Common;

namespace Prata.Application.Briefing;

public sealed record OpenTemplateEditCommand(Guid TemplateId) : ICommand<Unit>;

public sealed class OpenTemplateEditHandler(
    ITenantContext tenantContext,
    IBriefingTemplateRepository templates,
    IUnitOfWork uow
) : ICommandHandler<OpenTemplateEditCommand, Unit>
{
    public async Task<Result<Unit>> Handle(OpenTemplateEditCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var template = await templates.GetByIdAsync(tenantId, command.TemplateId, cancellationToken);
        if (template is null)
            return Error.Validation("BRIEFING_TEMPLATE_AUSENTE", "Template nao encontrado.");

        var result = template.AbrirEdicao();
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record CreateTemplateVersionCommand(Guid TemplateId) : ICommand<Guid>;

public sealed class CreateTemplateVersionHandler(
    ITenantContext tenantContext,
    IBriefingTemplateRepository templates,
    IUnitOfWork uow
) : ICommandHandler<CreateTemplateVersionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTemplateVersionCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var template = await templates.GetByIdAsync(tenantId, command.TemplateId, cancellationToken);
        if (template is null)
            return Error.Validation("BRIEFING_TEMPLATE_AUSENTE", "Template nao encontrado.");

        var next = template.CriarNovaVersao();
        if (next.IsFailure)
            return Result.Failure<Guid>(next.Error!.Value);

        await templates.AddAsync(next.Value, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return next.Value.Id;
    }
}

public sealed record AddTemplateQuestionCommand(
    Guid TemplateId,
    string Code,
    string Prompt,
    string Type,
    string Block,
    int SortOrder,
    bool IsRequired,
    bool IsSensitive,
    string? VisibleWhen,
    IReadOnlyList<QuestionOptionDto>? Options
) : ICommand<Unit>;

public sealed record QuestionOptionDto(string Code, string Label, int SortOrder, bool IsSuggestedChip);

public sealed class AddTemplateQuestionHandler(
    ITenantContext tenantContext,
    IBriefingTemplateRepository templates,
    IUnitOfWork uow
) : ICommandHandler<AddTemplateQuestionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AddTemplateQuestionCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var template = await templates.GetByIdAsync(tenantId, command.TemplateId, cancellationToken);
        if (template is null)
            return Error.Validation("BRIEFING_TEMPLATE_AUSENTE", "Template nao encontrado.");

        if (!Enum.TryParse<QuestionType>(command.Type, true, out var type))
            return Error.Validation("BRIEFING_TIPO_INVALIDO", "Tipo de pergunta invalido.");

        if (!Enum.TryParse<BriefingBlock>(command.Block, true, out var block))
            return Error.Validation("BRIEFING_BLOCO_INVALIDO", "Bloco invalido.");

        var question = Question.Create(
            tenantId,
            template.Id,
            command.Code,
            command.Prompt,
            type,
            block,
            command.SortOrder,
            command.IsRequired,
            command.IsSensitive,
            command.VisibleWhen
        );
        if (question.IsFailure)
            return Result.Failure<Unit>(question.Error!.Value);

        if (command.Options is not null)
        {
            foreach (var opt in command.Options)
            {
                var addOpt = question.Value.AdicionarOpcao(opt.Code, opt.Label, opt.SortOrder, opt.IsSuggestedChip);
                if (addOpt.IsFailure)
                    return addOpt;
            }
        }

        var add = template.AdicionarPergunta(question.Value);
        if (add.IsFailure)
            return add;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RemoveTemplateQuestionCommand(Guid TemplateId, string Code) : ICommand<Unit>;

public sealed class RemoveTemplateQuestionHandler(
    ITenantContext tenantContext,
    IBriefingTemplateRepository templates,
    IUnitOfWork uow
) : ICommandHandler<RemoveTemplateQuestionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RemoveTemplateQuestionCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var template = await templates.GetByIdAsync(tenantId, command.TemplateId, cancellationToken);
        if (template is null)
            return Error.Validation("BRIEFING_TEMPLATE_AUSENTE", "Template nao encontrado.");

        var result = template.RemoverPergunta(command.Code);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record PublishTemplateCommand(Guid TemplateId) : ICommand<Unit>;

public sealed class PublishTemplateHandler(
    ITenantContext tenantContext,
    IBriefingTemplateRepository templates,
    IUnitOfWork uow
) : ICommandHandler<PublishTemplateCommand, Unit>
{
    public async Task<Result<Unit>> Handle(PublishTemplateCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var template = await templates.GetByIdAsync(tenantId, command.TemplateId, cancellationToken);
        if (template is null)
            return Error.Validation("BRIEFING_TEMPLATE_AUSENTE", "Template nao encontrado.");

        var result = template.Publicar();
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Hint de UX do portal: B2 "nao sei" sugere teste das duas selfies (docs/07).</summary>
public static class BriefingUxHints
{
    public static bool SuggestSelfieSideTest(string questionCode, string? valueJson)
    {
        if (!string.Equals(questionCode, "B2", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(valueJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            var root = doc.RootElement;
            string? option = null;
            if (root.TryGetProperty("option", out var o))
                option = o.GetString();
            else if (root.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String)
                option = v.GetString();

            return string.Equals(option, "NAO_SEI", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
