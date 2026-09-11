using Prata.Application.Abstractions;
using Prata.Application.Briefing;
using Prata.Application.Common;
using Prata.Domain.Common;

namespace Prata.Api.Endpoints;

public static class TemplateEditorEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEditorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/studio/briefing-templates")
            .WithTags("BriefingTemplates")
            .RequireAuthorization();

        group.MapGet(
            "/",
            async (ITenantContext tenant, IBriefingTemplateRepository templates, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var list = await templates.ListByTenantAsync(tenantId, ct);
                return Results.Ok(
                    list.Select(t => new
                    {
                        t.Id,
                        t.Name,
                        t.ServiceTypeId,
                        t.Version,
                        t.IsPublished,
                        QuestionCount = t.Questions.Count,
                    })
                );
            }
        );

        group.MapGet(
            "/{id:guid}",
            async (Guid id, ITenantContext tenant, IBriefingTemplateRepository templates, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var template = await templates.GetByIdAsync(tenantId, id, ct);
                if (template is null)
                    return Results.NotFound();

                return Results.Ok(
                    new
                    {
                        template.Id,
                        template.Name,
                        template.ServiceTypeId,
                        template.Version,
                        template.IsPublished,
                        Questions = template
                            .Questions.OrderBy(q => q.SortOrder)
                            .Select(q => new
                            {
                                q.Code,
                                q.Prompt,
                                Type = q.Type.ToString(),
                                Block = q.Block.ToString(),
                                q.SortOrder,
                                q.IsRequired,
                                q.IsSensitive,
                                q.VisibleWhen,
                                Options = q.Options.Select(o => new
                                {
                                    o.Code,
                                    o.Label,
                                    o.SortOrder,
                                    o.IsSuggestedChip,
                                }),
                            }),
                    }
                );
            }
        );

        group.MapPost(
            "/{id:guid}/open-edit",
            async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new OpenTemplateEditCommand(id), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        group.MapPost(
            "/{id:guid}/new-version",
            async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new CreateTemplateVersionCommand(id), ct);
                return result.IsSuccess
                    ? Results.Created($"/v1/studio/briefing-templates/{result.Value}", new { id = result.Value })
                    : Problem(result.Error!.Value);
            }
        );

        group.MapPost(
            "/{id:guid}/questions",
            async (Guid id, AddQuestionBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(
                    new AddTemplateQuestionCommand(
                        id,
                        body.Code,
                        body.Prompt,
                        body.Type,
                        body.Block,
                        body.SortOrder,
                        body.IsRequired,
                        body.IsSensitive,
                        body.VisibleWhen,
                        body.Options
                            ?.Select(o => new QuestionOptionDto(o.Code, o.Label, o.SortOrder, o.IsSuggestedChip))
                            .ToList()
                    ),
                    ct
                );
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        group.MapDelete(
            "/{id:guid}/questions/{code}",
            async (Guid id, string code, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new RemoveTemplateQuestionCommand(id, code), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        group.MapPost(
            "/{id:guid}/publish",
            async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new PublishTemplateCommand(id), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        return app;
    }

    private static IResult Problem(Error error) =>
        Results.Json(new { type = error.Code, title = error.Message }, statusCode: StatusCodes.Status400BadRequest);
}

public sealed record AddQuestionBody(
    string Code,
    string Prompt,
    string Type,
    string Block,
    int SortOrder,
    bool IsRequired,
    bool IsSensitive,
    string? VisibleWhen,
    IReadOnlyList<AddOptionBody>? Options
);

public sealed record AddOptionBody(string Code, string Label, int SortOrder, bool IsSuggestedChip);
